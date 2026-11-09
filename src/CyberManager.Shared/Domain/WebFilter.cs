namespace CyberManager.Shared.Domain;

/// <summary>
/// Regla de NetFiltroWeb. Las reglas de tipo <see cref="WebFilterRuleKind.Domain"/>
/// bloquean un sitio exacto (y sus subdominios); las de tipo
/// <see cref="WebFilterRuleKind.Keyword"/> bloquean cualquier sitio que contenga
/// la palabra indicada en el nombre del host.
/// </summary>
public class WebFilterRule
{
    public int Id { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public WebFilterRuleKind Kind { get; set; } = WebFilterRuleKind.Domain;
    public bool Active { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
