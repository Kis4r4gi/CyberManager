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

        // Eliminación permanente: conserva las ventas y movimientos, pero borra la cuenta.
        g.MapDelete("/{id:int}/permanent", async (int id, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (id == principal.UserId()) throw new AppException("No puede eliminar su propio usuario.");
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id) ?? throw AppException.NotFound("Usuario");

            if (user.Role == UserRole.Admin &&
                await db.Users.CountAsync(u => u.Active && u.Role == UserRole.Admin) <= 1)
                throw new AppException("No se puede eliminar el último administrador activo.");

            if (await db.CashSessions.AnyAsync(c => c.OpenedByUserId == id && c.IsOpen))
                throw new AppException("El usuario tiene una caja abierta. Ciérrela antes de eliminar la cuenta.");

            // Las referencias opcionales se limpian para no dejar identificadores huérfanos.
            await db.Sessions.Where(s => s.OpenedByUserId == id || s.ClosedByUserId == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.OpenedByUserId, (int?)null)
                    .SetProperty(x => x.ClosedByUserId, (int?)null));
            await db.CashSessions.Where(c => c.ClosedByUserId == id)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.ClosedByUserId, (int?)null));
            await db.CashMovements.Where(m => m.UserId == id)
                .ExecuteUpdateAsync(m => m.SetProperty(x => x.UserId, (int?)null));
            await db.Sales.Where(s => s.UserId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, (int?)null));
            await db.StockMovements.Where(m => m.UserId == id)
                .ExecuteUpdateAsync(m => m.SetProperty(x => x.UserId, (int?)null));
            await db.Purchases.Where(p => p.UserId == id)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.UserId, (int?)null));
            await db.CustomerTransactions.Where(t => t.UserId == id)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.UserId, (int?)null));
            await db.SupplierPayments.Where(p => p.UserId == id)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.UserId, (int?)null));
            await db.AuditLogs.Where(a => a.UserId == id)
                .ExecuteUpdateAsync(a => a.SetProperty(x => x.UserId, (int?)null));
            await db.ExchangeRates.Where(r => r.CreatedByUserId == id)
                .ExecuteUpdateAsync(r => r.SetProperty(x => x.CreatedByUserId, (int?)null));

            db.Users.Remove(user);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
