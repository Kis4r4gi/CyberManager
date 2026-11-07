using CyberManager.Server.Services;

namespace CyberManager.Server;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _log;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.StatusCode = ex.StatusCode;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error no controlado en {Path}", context.Request.Path);
            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Ocurrió un error interno. Revise el registro del servidor."
            });
        }
    }
}
