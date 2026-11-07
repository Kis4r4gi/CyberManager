using System.Security.Claims;
using CyberManager.Server.Data;
using CyberManager.Server.Security;
using CyberManager.Server.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Autenticación");

        g.MapPost("/login", async (LoginRequest req, AppDbContext db, TokenService tokens) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                throw new AppException("Usuario y contraseña son obligatorios.");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username && u.Active);
            if (user is null || !PasswordHasher.Verify(req.Password, user.PasswordHash, user.PasswordSalt))
                throw new AppException("Usuario o contraseña incorrectos.", 401);

            user.LastLoginUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var (token, expires) = tokens.CreateToken(user);
            return Results.Ok(new LoginResponse(token, user.Id, user.Username, user.FullName, user.Role, expires));
        }).AllowAnonymous();

        g.MapGet("/me", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var id = principal.UserId();
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id)
                       ?? throw AppException.NotFound("Usuario");
            return Results.Ok(new UserDto(user.Id, user.Username, user.FullName, user.Role, user.Active, user.LastLoginUtc));
        }).RequireAuthorization();

        g.MapPost("/change-password", async (ChangePasswordRequest req, ClaimsPrincipal principal,
            AppDbContext db) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == principal.UserId())
                       ?? throw AppException.NotFound("Usuario");
            if (!PasswordHasher.Verify(req.CurrentPassword, user.PasswordHash, user.PasswordSalt))
                throw new AppException("La contraseña actual no es correcta.");
            if (req.NewPassword.Length < 6)
                throw new AppException("La nueva contraseña debe tener al menos 6 caracteres.");

            var (hash, salt) = PasswordHasher.Create(req.NewPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/users").WithTags("Usuarios").RequireAuthorization("Admin");

        g.MapGet("/", async (AppDbContext db) => Results.Ok(await db.Users.AsNoTracking()
            .OrderBy(u => u.Username)
            .Select(u => new UserDto(u.Id, u.Username, u.FullName, u.Role, u.Active, u.LastLoginUtc))
            .ToListAsync()));

        g.MapPost("/", async (CreateUserRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username)) throw new AppException("El usuario es obligatorio.");
            if (req.Password.Length < 6) throw new AppException("La contraseña debe tener al menos 6 caracteres.");
            if (await db.Users.AnyAsync(u => u.Username == req.Username))
                throw new AppException("Ese usuario ya existe.", 409);

            var (hash, salt) = PasswordHasher.Create(req.Password);
            var user = new Shared.Domain.UserAccount
            {
                Username = req.Username.Trim(),
                FullName = req.FullName.Trim(),
                Role = req.Role,
                PasswordHash = hash,
                PasswordSalt = salt
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Ok(new UserDto(user.Id, user.Username, user.FullName, user.Role, user.Active, null));
        });

        g.MapPut("/{id:int}", async (int id, UpdateUserRequest req, AppDbContext db) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id) ?? throw AppException.NotFound("Usuario");
            user.FullName = req.FullName;
            user.Role = req.Role;
            user.Active = req.Active;
            if (!string.IsNullOrWhiteSpace(req.Password))
            {
                if (req.Password.Length < 6) throw new AppException("La contraseña debe tener al menos 6 caracteres.");
                var (hash, salt) = PasswordHasher.Create(req.Password);
                user.PasswordHash = hash;
                user.PasswordSalt = salt;
            }
            await db.SaveChangesAsync();
            return Results.Ok(new UserDto(user.Id, user.Username, user.FullName, user.Role, user.Active, user.LastLoginUtc));
        });

        g.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (id == principal.UserId()) throw new AppException("No puede desactivar su propio usuario.");
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id) ?? throw AppException.NotFound("Usuario");
            user.Active = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
