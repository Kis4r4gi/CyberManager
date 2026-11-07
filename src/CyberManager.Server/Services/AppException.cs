namespace CyberManager.Server.Services;

/// <summary>Error de negocio controlado que se traduce a una respuesta HTTP concreta.</summary>
public class AppException : Exception
{
    public int StatusCode { get; }

    public AppException(string message, int statusCode = 400) : base(message) => StatusCode = statusCode;

    public static AppException NotFound(string what) => new($"{what} no encontrado.", 404);
    public static AppException Forbidden(string message = "No tiene permiso para esta operación.") => new(message, 403);
}
