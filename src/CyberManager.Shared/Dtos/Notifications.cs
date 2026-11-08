namespace CyberManager.Shared.Dtos;

/// <summary>Solicitud para enviar el cierre diario por correo electrónico.</summary>
public sealed record DailyReportEmailRequest(DateTime Date, bool Force = false);

/// <summary>Resultado del envío del cierre diario o de un correo de prueba.</summary>
public sealed record DailyReportEmailResultDto(
    bool Sent,
    bool Skipped,
    string Message,
    DateTime? SentUtc = null,
    string? Recipients = null);

/// <summary>Correo de prueba. Si no se indica destinatario se usan los configurados.</summary>
public sealed record TestEmailRequest(string? Recipient = null);
