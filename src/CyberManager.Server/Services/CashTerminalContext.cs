using Microsoft.AspNetCore.Http;

namespace CyberManager.Server.Services;

/// <summary>
/// Identifica la caja o terminal que realiza la petición. La consola envía el nombre
/// en la cabecera <c>X-Cash-Terminal</c>; si falta, se usa <c>PRINCIPAL</c> para
/// mantener el comportamiento de las instalaciones de una sola caja.
/// </summary>
public class CashTerminalContext
{
    public const string HeaderName = "X-Cash-Terminal";
    public const string DefaultTerminal = "PRINCIPAL";
    private const int MaxLength = 40;

    private readonly IHttpContextAccessor _http;

    public CashTerminalContext(IHttpContextAccessor http) => _http = http;

    /// <summary>Nombre normalizado de la caja que hace la petición.</summary>
    public string Name => Normalize(_http.HttpContext?.Request.Headers[HeaderName].ToString());

    public static string Normalize(string? value)
    {
        var name = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (name.Length == 0) return DefaultTerminal;
        return name.Length <= MaxLength ? name : name[..MaxLength];
    }
}
