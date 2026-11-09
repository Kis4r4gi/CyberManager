namespace CyberManager.Shared.Dtos;

public record WebFilterRuleDto(
    int Id, string Pattern, WebFilterRuleKind Kind, bool Active, string? Notes,
    DateTime CreatedUtc, DateTime UpdatedUtc);

public record SaveWebFilterRuleRequest(
    string Pattern, WebFilterRuleKind Kind, bool Active, string? Notes);

/// <summary>Lista que el agente descarga periódicamente para aplicar el filtro.</summary>
public record WebFilterPlaylistDto(List<WebFilterRuleDto> Rules, DateTime GeneratedUtc,
    bool ProxyEnabled = false, int ProxyPort = 8877);
