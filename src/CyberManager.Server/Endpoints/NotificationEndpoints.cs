using CyberManager.Server.Services;
using CyberManager.Shared.Dtos;

namespace CyberManager.Server.Endpoints;

/// <summary>Envío del cierre diario y de correos de prueba.</summary>
public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reports/daily-closing")
            .WithTags("Reportes por correo")
            .RequireAuthorization();

        g.MapPost("/email", async (DateTime? date, bool? force, EmailService email) =>
            Results.Ok(await email.SendDailyClosingAsync(date ?? DateTime.Today, force == true)))
            .RequireAuthorization("Admin");

        g.MapPost("/email/test", async (TestEmailRequest? request, EmailService email) =>
            Results.Ok(await email.SendTestAsync(request?.Recipient)))
            .RequireAuthorization("Admin");

        return app;
    }
}
