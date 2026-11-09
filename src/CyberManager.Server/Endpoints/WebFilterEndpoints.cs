using CyberManager.Server.Data;
using CyberManager.Server.Services;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Endpoints;

/// <summary>
/// Administración del módulo NetFiltroWeb. Las reglas se descargan los agentes
/// y se aplican en cada equipo; la lista inicial se siembra una sola vez.
/// </summary>
public static class WebFilterEndpoints
{
    public static IEndpointRouteBuilder MapWebFilterEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/webfilter").WithTags("NetFiltroWeb").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db) =>
        {
            var rules = await db.WebFilterRules.AsNoTracking()
                .OrderBy(r => r.Kind).ThenBy(r => r.Pattern)
                .Select(r => new WebFilterRuleDto(r.Id, r.Pattern, r.Kind, r.Active, r.Notes,
                    r.CreatedUtc, r.UpdatedUtc))
                .ToListAsync();
            return Results.Ok(rules);
        });

        g.MapPost("/", async (SaveWebFilterRuleRequest req, AppDbContext db, RealtimeService realtime,
            SettingsService settings) =>
        {
            var pattern = Normalize(req.Pattern);
            if (string.IsNullOrWhiteSpace(pattern))
                throw new AppException("Indique el sitio o la palabra que desea bloquear.");
            if (await db.WebFilterRules.AnyAsync(r => r.Pattern.ToLower() == pattern.ToLower()))
                throw new AppException("Ya existe una regla con ese valor.", 409);

            var now = DateTime.UtcNow;
            var rule = new WebFilterRule
            {
                Pattern = pattern,
                Kind = req.Kind,
                Active = req.Active,
                Notes = req.Notes,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            db.WebFilterRules.Add(rule);
            await db.SaveChangesAsync();
            await NotifyChangeAsync(db, realtime, settings);
            return Results.Ok(ToDto(rule));
        }).RequireAuthorization("Admin");

        g.MapPut("/{id:int}", async (int id, SaveWebFilterRuleRequest req, AppDbContext db,
            RealtimeService realtime, SettingsService settings) =>
        {
            var rule = await db.WebFilterRules.FirstOrDefaultAsync(r => r.Id == id)
                       ?? throw AppException.NotFound("Regla de NetFiltroWeb");

            var pattern = Normalize(req.Pattern);
            if (string.IsNullOrWhiteSpace(pattern))
                throw new AppException("Indique el sitio o la palabra que desea bloquear.");
            if (await db.WebFilterRules.AnyAsync(r => r.Id != id && r.Pattern.ToLower() == pattern.ToLower()))
                throw new AppException("Ya existe una regla con ese valor.", 409);

            rule.Pattern = pattern;
            rule.Kind = req.Kind;
            rule.Active = req.Active;
            rule.Notes = req.Notes;
            rule.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await NotifyChangeAsync(db, realtime, settings);
            return Results.Ok(ToDto(rule));
        }).RequireAuthorization("Admin");

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, RealtimeService realtime,
            SettingsService settings) =>
        {
            var rule = await db.WebFilterRules.FirstOrDefaultAsync(r => r.Id == id)
                       ?? throw AppException.NotFound("Regla de NetFiltroWeb");
            db.WebFilterRules.Remove(rule);
            await db.SaveChangesAsync();
            await NotifyChangeAsync(db, realtime, settings);
            return Results.NoContent();
        }).RequireAuthorization("Admin");

        // Restaura la lista de fábrica sin duplicar las reglas ya existentes.
        g.MapPost("/restore", async (AppDbContext db, RealtimeService realtime, SettingsService settings) =>
        {
            var existing = await db.WebFilterRules.AsNoTracking().Select(r => r.Pattern).ToListAsync();
            var known = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;
            var added = 0;

            foreach (var (pattern, kind, notes) in DbSeeder.DefaultWebFilterRules)
            {
                if (!known.Add(pattern)) continue;
                db.WebFilterRules.Add(new WebFilterRule
                {
                    Pattern = pattern,
                    Kind = kind,
                    Active = true,
                    Notes = notes,
                    CreatedUtc = now,
                    UpdatedUtc = now
                });
                added++;
            }

            await db.SaveChangesAsync();
            await NotifyChangeAsync(db, realtime, settings);
            return Results.Ok(new { added });
        }).RequireAuthorization("Admin");

        return app;
    }

    private static string Normalize(string value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (text.StartsWith("http://", StringComparison.Ordinal)) text = text[7..];
        if (text.StartsWith("https://", StringComparison.Ordinal)) text = text[8..];
        var slash = text.IndexOf('/');
        if (slash >= 0) text = text[..slash];
        if (text.StartsWith("www.", StringComparison.Ordinal)) text = text[4..];
        return text.Trim('.', ' ');
    }

    private static WebFilterRuleDto ToDto(WebFilterRule r) =>
        new(r.Id, r.Pattern, r.Kind, r.Active, r.Notes, r.CreatedUtc, r.UpdatedUtc);

    private static async Task NotifyChangeAsync(AppDbContext db, RealtimeService realtime,
        SettingsService settings)
    {
        var rules = await db.WebFilterRules.AsNoTracking().Where(r => r.Active)
            .OrderBy(r => r.Pattern)
            .Select(r => new WebFilterRuleDto(r.Id, r.Pattern, r.Kind, r.Active, r.Notes,
                r.CreatedUtc, r.UpdatedUtc))
            .ToListAsync();
        var proxyEnabled = await settings.GetBoolAsync(SettingsKeys.WebFilterProxyEnabled, true);
        var proxyPort = await settings.GetIntAsync(SettingsKeys.WebFilterProxyPort, 8877);
        await realtime.WebFilterChangedAsync(new WebFilterPlaylistDto(rules, DateTime.UtcNow,
            proxyEnabled, Math.Clamp(proxyPort, 1024, 65535)));
    }
}
