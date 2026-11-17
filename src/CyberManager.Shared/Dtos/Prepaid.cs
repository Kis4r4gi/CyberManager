namespace CyberManager.Shared.Dtos;

/// <summary>Datos que la pantalla de bloqueo muestra antes de iniciar sesión.</summary>
public record PrepaidInfoDto(
    string BusinessName, decimal ExchangeRate, string LocalCurrencySymbol,
    List<TimePackageDto> Packages);

/// <summary>Resumen de la cuenta prepago de un cliente.</summary>
public record PrepaidAccountDto(
    int CustomerId, string Name, string Username, int PrepaidMinutes, decimal Balance);

public record PrepaidRegisterRequest(
    string Name, string Username, string Password, string? Phone, string? Email);

public record PrepaidLoginRequest(string Username, string Password);

/// <summary>Respuesta del inicio de sesión prepago. <c>ActiveSessionId</c> no es null si el
/// cliente ya tiene una sesión abierta en este equipo.</summary>
public record PrepaidLoginResponse(
    PrepaidAccountDto Account, int? ActiveSessionId, int ActiveSessionRemainingMinutes);

/// <summary>Compra de un paquete de tiempo con el saldo monetario de la cuenta.</summary>
public record PrepaidRechargeRequest(string Username, string Password, int PackageId);

public record PrepaidRechargeResultDto(
    PrepaidAccountDto Account, string PackageName, int AddedMinutes, bool ExtendedSession);

/// <summary>Inicio de una sesión prepago usando los minutos disponibles de la cuenta.</summary>
public record PrepaidStartRequest(string Username, string Password, int Minutes);

public record PrepaidStartResultDto(
    PrepaidAccountDto Account, int SessionId, int UsedMinutes);

/// <summary>Cierre de la sesión prepago con devolución de los minutos no usados.</summary>
public record PrepaidFinishResultDto(
    PrepaidAccountDto Account, int RefundedMinutes);
