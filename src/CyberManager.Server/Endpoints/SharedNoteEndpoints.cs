using CyberManager.Server.Data;
using CyberManager.Server.Services;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Endpoints;

/// <summary>Notas breves compartidas por todos los usuarios de la consola.</summary>
public static class SharedNoteEndpoints
{
    private const int MaxTextLength = 1200;
    private const int MaxNotes = 100;

    public static IEndpointRouteBuilder MapSharedNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/shared-notes").WithTags("Notas")
            .RequireAuthorization();

        g.MapGet("/", async (AppDbContext db) =>
        {
            var notes = await db.SharedNotes.AsNoTracking()
                .OrderByDescending(n => n.CreatedUtc)
                .ThenByDescending(n => n.Id)
                .Take(MaxNotes)
                .ToListAsync();

            return Results.Ok(notes.Select(ToDto).ToList());
        });

        g.MapPost("/", async (SaveSharedNoteRequest req, AppDbContext db,
            HttpContext http, RealtimeService realtime) =>
        {
            var text = (req.Text ?? string.Empty).Trim();
            if (text.Length == 0)
                throw new AppException("Escriba una nota antes de publicarla.");
            if (text.Length > MaxTextLength)
                throw new AppException($"La nota no puede superar los {MaxTextLength} caracteres.");

            var userId = http.User.UserId();
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

            var note = new SharedNote
            {
                Text = text,
                UserId = userId > 0 ? userId : null,
                AuthorName = string.IsNullOrWhiteSpace(user?.FullName)
                    ? http.User.UserName()
                    : user!.FullName,
                CreatedUtc = DateTime.UtcNow
            };

            db.SharedNotes.Add(note);
            await db.SaveChangesAsync();

            var dto = ToDto(note);
            await realtime.BroadcastEventAsync("SharedNoteAdded",
                $"{note.AuthorName}: {note.Text}");

            return Results.Ok(dto);
        });

        g.MapDelete("/{id:long}", async (long id, AppDbContext db, RealtimeService realtime) =>
        {
            var note = await db.SharedNotes.FirstOrDefaultAsync(n => n.Id == id)
                       ?? throw AppException.NotFound("Nota");
            db.SharedNotes.Remove(note);
            await db.SaveChangesAsync();

            await realtime.BroadcastEventAsync("SharedNoteDeleted",
                $"{note.AuthorName}: {note.Text}");

            return Results.NoContent();
        }).RequireAuthorization("Admin");

        return app;
    }

    private static SharedNoteDto ToDto(SharedNote note)
        => new(note.Id, note.Text, note.AuthorName, note.CreatedUtc);
}
