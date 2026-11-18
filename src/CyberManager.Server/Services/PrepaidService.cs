using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CyberManager.Server.Data;
using CyberManager.Server.Security;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

/// <summary>
/// Cuentas prepago de autoservicio: registro, inicio de sesión, compra de paquetes de
/// tiempo e inicio/cierre de sesiones en la estación. Reutiliza el saldo monetario del
/// cliente (<see cref="Customer.Balance"/>) y el monedero de minutos
/// (<see cref="Customer.PrepaidMinutes"/>).
/// </summary>
public class PrepaidService
{
    private static readonly Regex UsernamePattern =
        new("^[a-z0-9][a-z0-9._-]{2,31}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Intentos fallidos por usuario: tras 5 fallos se bloquea 60 segundos.</summary>
    private static readonly ConcurrentDictionary<string, LoginAttempt> LoginAttempts = new();
    private const int MaxLoginFailures = 5;
    private static readonly TimeSpan LoginLockout = TimeSpan.FromSeconds(60);

    private readonly record struct LoginAttempt(int Failures, DateTime LockedUntilUtc);

    private readonly AppDbContext _db;
    private readonly SettingsService _settings;
    private readonly ExchangeRateService _rates;
    private readonly RealtimeService _realtime;
    private readonly SessionService _sessions;

    public PrepaidService(AppDbContext db, SettingsService settings, ExchangeRateService rates,
        RealtimeService realtime, SessionService sessions)
    {
        _db = db;
        _settings = settings;
        _rates = rates;
        _realtime = realtime;
        _sessions = sessions;
    }

    public async Task<PrepaidInfoDto> InfoAsync()
    {
        var packages = await _db.TimePackages.AsNoTracking()
            .Where(p => p.Active)
            .Select(p => new TimePackageDto(p.Id, p.Name, p.Minutes, p.BonusMinutes, p.Price, p.Active))
            .ToListAsync();

        return new PrepaidInfoDto(
            await _settings.GetAsync(SettingsKeys.BusinessName, "CyberManager"),
            await _rates.GetRateAsync(DateTime.UtcNow),
            await _settings.GetAsync(SettingsKeys.LocalCurrencySymbol, "Bs"),
            packages.OrderBy(p => p.Price).ThenBy(p => p.Minutes).ToList());
    }

    public async Task<PrepaidAccountDto> RegisterAsync(PrepaidRegisterRequest req)
    {
        var name = (req.Name ?? string.Empty).Trim();
        var username = NormalizeUsername(req.Username);
        var password = req.Password ?? string.Empty;

        if (name.Length < 2)
            throw new AppException("Escriba su nombre completo.");
        if (!UsernamePattern.IsMatch(username))
            throw new AppException(
                "El usuario debe tener entre 3 y 32 caracteres: letras, números, punto, guion o guion bajo.");
        if (password.Length < 4)
            throw new AppException("La contraseña debe tener al menos 4 caracteres.");
        if (await _db.Customers.AnyAsync(c => c.Username == username))
            throw new AppException("Ese usuario ya está registrado.", 409);

        var (hash, salt) = PasswordHasher.Create(password);
        var customer = new Customer
        {
            Name = name,
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            IsMember = true,
            Active = true,
            PrepaidMinutes = 0,
            Balance = 0
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        return ToDto(customer);
    }

    public async Task<PrepaidLoginResponse> LoginAsync(int stationId, PrepaidLoginRequest req)
    {
        var customer = await AuthenticateAsync(req.Username, req.Password);
        var session = await ActiveSessionAsync(stationId, customer.Id);
        var remaining = session is null
            ? 0
            : Math.Max(SessionCalculator.RemainingMinutes(session,
                SessionCalculator.ElapsedMinutes(session, DateTime.UtcNow)), 0);

        return new PrepaidLoginResponse(ToDto(customer), session?.Id, remaining);
    }

    public async Task<PrepaidRechargeResultDto> RechargeAsync(int stationId, PrepaidRechargeRequest req)
    {
        var customer = await AuthenticateAsync(req.Username, req.Password);
        var package = await _db.TimePackages
            .FirstOrDefaultAsync(p => p.Id == req.PackageId && p.Active)
            ?? throw AppException.NotFound("Paquete de tiempo");

        var totalMinutes = package.Minutes + package.BonusMinutes;
        if (totalMinutes <= 0)
            throw new AppException("El paquete no tiene minutos configurados.");

        if (customer.Balance < package.Price)
            throw new AppException(
                $"Saldo insuficiente. Disponible: {customer.Balance:F2} USD. Recargue su saldo en caja.");

        customer.Balance = Math.Round(customer.Balance - package.Price, 2, MidpointRounding.AwayFromZero);
        _db.CustomerTransactions.Add(new CustomerTransaction
        {
            CustomerId = customer.Id,
            Type = CustomerTransactionType.Consumption,
            Amount = -package.Price,
            BalanceAfter = customer.Balance,
            Description = $"Paquete de tiempo: {package.Name}",
            UserId = null
        });

        // Si el cliente ya está jugando en este equipo, los minutos se suman a la
        // sesión activa; de lo contrario quedan en su monedero prepago.
        var session = await ActiveSessionAsync(stationId, customer.Id);
        var extended = session is not null;
        if (session is not null)
        {
            session.PlannedMinutes += totalMinutes;
            session.AmountCharged = Math.Round(session.AmountCharged + package.Price, 2,
                MidpointRounding.AwayFromZero);
        }
        else
        {
            customer.PrepaidMinutes += totalMinutes;
        }

        await _db.SaveChangesAsync();

        if (session is not null)
        {
            var station = await _db.Stations.FirstAsync(s => s.Id == stationId);

            // Si al recargar todavía le quedan minutos, el equipo vuelve a quedar
            // utilizable: sin esto el cliente pagaba y seguía bloqueado.
            var elapsed = SessionCalculator.ElapsedMinutes(session, DateTime.UtcNow);
            if (SessionCalculator.RemainingMinutes(session, elapsed) > 0 && station.IsLocked)
            {
                station.IsLocked = false;
                await _db.SaveChangesAsync();
                await _realtime.SendAgentCommandAsync(station.Id, AgentCommandType.Unlock);
                await _realtime.SendStationCommandAsync(station.Id, AgentCommandType.Unlock);
            }

            await NotifyStationAsync(station, session);
        }

        await _realtime.BroadcastEventAsync("PrepaidRecharge",
            $"{customer.Name}: +{totalMinutes} min ({package.Name})", customer.Id);

        return new PrepaidRechargeResultDto(ToDto(customer), package.Name, totalMinutes, extended);
    }

    public async Task<PrepaidStartResultDto> StartAsync(int stationId, PrepaidStartRequest req)
    {
        var customer = await AuthenticateAsync(req.Username, req.Password);
        if (customer.PrepaidMinutes <= 0)
            throw new AppException("No tiene minutos disponibles. Compre un paquete de tiempo.");

        var station = await _db.Stations.Include(s => s.Rate)
            .FirstOrDefaultAsync(s => s.Id == stationId)
            ?? throw AppException.NotFound("Equipo");

        if (!station.Active)
            throw new AppException("El equipo está deshabilitado.");
        if (station.Status is StationStatus.InUse or StationStatus.Paused)
            throw new AppException("El equipo ya tiene una sesión activa.", 409);
        if (station.Status == StationStatus.Maintenance)
            throw new AppException("El equipo está en mantenimiento.");

        var rate = station.Rate
                   ?? await _db.Rates.FirstOrDefaultAsync(r => r.Active)
                   ?? throw new AppException("No hay tarifas configuradas.");
        if (rate.PricePerHour <= 0)
            throw new AppException("La tarifa del equipo no está configurada.");

        var minutes = req.Minutes <= 0
            ? customer.PrepaidMinutes
            : Math.Min(req.Minutes, customer.PrepaidMinutes);
        if (minutes <= 0)
            throw new AppException("Indique cuántos minutos desea usar.");

        var amount = Math.Round(rate.PricePerHour * minutes / 60m, 2, MidpointRounding.AwayFromZero);
        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync();

        customer.PrepaidMinutes -= minutes;
        var session = new Session
        {
            StationId = station.Id,
            CustomerId = customer.Id,
            Type = SessionType.Prepaid,
            Status = SessionStatus.Active,
            StartedUtc = now,
            PlannedMinutes = minutes,
            RatePerHour = rate.PricePerHour,
            MinimumMinutes = rate.MinimumMinutes,
            BillingBlockMinutes = rate.BillingBlockMinutes,
            AmountCharged = amount,
            Notes = "Sesión prepago desde la estación"
        };
        _db.Sessions.Add(session);

        station.Status = StationStatus.InUse;
        station.IsLocked = false;
        await _db.SaveChangesAsync();
        station.CurrentSessionId = session.Id;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        await _realtime.SendAgentCommandAsync(station.Id, AgentCommandType.Unlock);
        await _realtime.SendStationCommandAsync(station.Id, AgentCommandType.Unlock);
        await NotifyStationAsync(station, session);
        await _realtime.BroadcastEventAsync("SessionStarted",
            $"{station.Name}: sesión prepago de {customer.Name} ({minutes} min)", session.Id);

        return new PrepaidStartResultDto(ToDto(customer), session.Id, minutes);
    }

    /// <summary>
    /// Cierra la sesión prepago del equipo y devuelve al monedero los minutos no usados.
    /// Los consumos pendientes se cobran al saldo del cliente.
    /// </summary>
    public async Task<PrepaidFinishResultDto> FinishAsync(int stationId)
    {
        var session = await _db.Sessions.Include(s => s.Station).Include(s => s.Customer)
            .Where(s => s.StationId == stationId && s.Type == SessionType.Prepaid
                        && (s.Status == SessionStatus.Active || s.Status == SessionStatus.Paused))
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync()
            ?? throw new AppException("No hay una sesión prepago activa en este equipo.", 409);

        if (session.CustomerId is not int customerId)
            throw new AppException("La sesión prepago no tiene una cuenta asociada.", 409);

        var elapsed = SessionCalculator.BilledMinutes(session, DateTime.UtcNow);
        var refund = Math.Max(session.PlannedMinutes - elapsed, 0);

        await _sessions.StopAsync(new StopSessionRequest(
            session.Id, PaymentMethod.CustomerBalance, ChargeTime: false,
            "Cierre de sesión prepago"), userId: 0);

        var customer = session.Customer ?? await _db.Customers.FirstAsync(c => c.Id == customerId);
        if (refund > 0)
        {
            customer.PrepaidMinutes += refund;
            await _db.SaveChangesAsync();
        }

        await _realtime.BroadcastEventAsync("SessionEnded",
            $"{session.Station?.Name ?? "Equipo"}: sesión prepago finalizada (+{refund} min)",
            session.Id);

        return new PrepaidFinishResultDto(ToDto(customer), refund);
    }

    private async Task<Customer> AuthenticateAsync(string? username, string? password)
    {
        var normalized = NormalizeUsername(username);
        var key = normalized.Length > 0 ? normalized : "?";

        if (LoginAttempts.TryGetValue(key, out var attempt) && attempt.LockedUntilUtc > DateTime.UtcNow)
        {
            var seconds = Math.Max(1,
                (int)Math.Ceiling((attempt.LockedUntilUtc - DateTime.UtcNow).TotalSeconds));
            throw new AppException($"Demasiados intentos fallidos. Espere {seconds} segundos.", 429);
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Username == normalized);
        var valid = customer is not null && customer.Active
            && !string.IsNullOrWhiteSpace(customer.PasswordHash)
            && !string.IsNullOrWhiteSpace(customer.PasswordSalt)
            && PasswordHasher.Verify(password ?? string.Empty,
                customer.PasswordHash, customer.PasswordSalt);

        if (!valid)
        {
            RegisterFailure(key);
            throw new AppException("Usuario o contraseña incorrectos.", 401);
        }

        LoginAttempts.TryRemove(key, out _);
        return customer!;
    }

    /// <summary>Cuenta los fallos seguidos y bloquea la cuenta temporalmente al llegar al límite.</summary>
    private static void RegisterFailure(string key)
    {
        var now = DateTime.UtcNow;
        LoginAttempts.AddOrUpdate(
            key,
            _ => new LoginAttempt(1, DateTime.MinValue),
            (_, current) =>
            {
                var failures = current.LockedUntilUtc > now ? current.Failures : current.Failures + 1;
                return failures >= MaxLoginFailures
                    ? new LoginAttempt(0, now + LoginLockout)
                    : new LoginAttempt(failures, DateTime.MinValue);
            });
    }

    private Task<Session?> ActiveSessionAsync(int stationId, int customerId)
        => _db.Sessions
            .Where(s => s.StationId == stationId && s.CustomerId == customerId
                        && (s.Status == SessionStatus.Active || s.Status == SessionStatus.Paused))
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync();

    private async Task NotifyStationAsync(Station station, Session session)
    {
        var elapsed = SessionCalculator.ElapsedMinutes(session, DateTime.UtcNow);
        var consumption = (await _db.Sales.AsNoTracking()
                .Where(s => s.SessionId == session.Id && s.Status == SaleStatus.Completed)
                .Select(s => s.Total)
                .ToListAsync())
            .Sum();
        var total = SessionCalculator.TimeAmount(session, elapsed) + consumption;

        await _realtime.StationChangedAsync(new StationStateEvent(
            station.Id, station.Status, session.Id, elapsed,
            SessionCalculator.RemainingMinutes(session, elapsed),
            Math.Round(total, 2), SessionCalculator.IsOvertime(session, elapsed)));
    }

    private static string NormalizeUsername(string? username)
        => (username ?? string.Empty).Trim().ToLowerInvariant();

    private static PrepaidAccountDto ToDto(Customer c)
        => new(c.Id, c.Name, c.Username ?? string.Empty, c.PrepaidMinutes, c.Balance);
}
