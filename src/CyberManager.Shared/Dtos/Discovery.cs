namespace CyberManager.Shared.Dtos;

/// <summary>Estación encontrada durante el escaneo de la red local.</summary>
public record DiscoveredStationDto(
    string Role,
    int StationId,
    string? Token,
    string ServerUrl,
    string MachineName,
    string IpAddress,
    string MacAddress,
    string Version,
    DateTime SeenUtc,
    int? MatchedStationId,
    string? MatchedStationName,
    bool IsNew,
    bool NeedsToken);

/// <summary>Equipo seleccionado por el administrador para darlo de alta o actualizarlo.</summary>
public record DiscoveryApplyItem(
    string Role,
    int StationId,
    string? Token,
    string MachineName,
    string IpAddress,
    string MacAddress,
    string Version);

public record ApplyDiscoveryRequest(List<DiscoveryApplyItem> Stations);

public record DiscoveryApplyResult(
    int Created,
    int Updated,
    int Configured,
    List<string> Messages);

/// <summary>Paquete del cliente que el servidor guarda y distribuye a las estaciones.</summary>
public record ClientUpdateManifestDto(
    string Version,
    string FileName,
    long Size,
    string Sha256,
    DateTime? PublishedUtc);

/// <summary>Estado del paquete de actualización y de las versiones reportadas por los equipos.</summary>
public record ClientUpdateStatusDto(
    string ServerVersion,
    string? PackageVersion,
    bool HasPackage,
    long PackageSize,
    string? Sha256,
    DateTime? PublishedUtc,
    int Stations,
    int UpToDate,
    int Outdated,
    int Unknown,
    string? Message);
