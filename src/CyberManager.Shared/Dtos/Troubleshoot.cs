namespace CyberManager.Shared.Dtos;

/// <summary>Gravedad de un hallazgo del solucionador de problemas.</summary>
public enum TroubleshootSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

/// <summary>Opciones de una ejecución del diagnóstico.</summary>
/// <param name="IncludeLogs">Incluye un extracto sanitizado de los registros del servidor.</param>
/// <param name="UseAi">Pide una explicación al proveedor de IA en la nube.</param>
public record TroubleshootRequestDto(bool IncludeLogs = true, bool UseAi = true);

/// <summary>Hallazgo determinista obtenido del estado del servidor y los equipos.</summary>
public record TroubleshootFindingDto(
    TroubleshootSeverity Severity,
    string Title,
    string Detail,
    string Recommendation,
    int? StationId = null);

/// <summary>Informe completo: reglas locales + explicación opcional de IA en la nube.</summary>
public record TroubleshootReportDto(
    DateTime GeneratedUtc,
    string ServerVersion,
    int Stations,
    int OnlineStations,
    bool AiConfigured,
    bool AiUsed,
    string? AiError,
    string Summary,
    string AiExplanation,
    List<TroubleshootFindingDto> Findings,
    List<string> LogExcerpts);
