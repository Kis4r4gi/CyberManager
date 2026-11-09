using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Server.Services;

/// <summary>
/// Envía el cierre diario por correo electrónico usando la configuración SMTP
/// guardada en la tabla de configuración. El envío nunca debe bloquear ni
/// interrumpir el cierre: los errores se devuelven como mensajes controlados.
/// </summary>
public class EmailService
{
    private readonly SettingsService _settings;
    private readonly ReportService _reports;

    public EmailService(SettingsService settings, ReportService reports)
    {
        _settings = settings;
        _reports = reports;
    }

    /// <summary>Envía el cierre del día indicado. Si ya se envió, se omite salvo que force sea true.</summary>
    public async Task<DailyReportEmailResultDto> SendDailyClosingAsync(DateTime date, bool force)
    {
        var enabled = await _settings.GetBoolAsync(SettingsKeys.DailyReportEmailEnabled);
        if (!enabled)
            return new DailyReportEmailResultDto(false, true,
                "El envío del cierre diario por correo está desactivado en Configuración.");

        var recipients = ParseRecipients(await _settings.GetAsync(SettingsKeys.DailyReportEmailRecipients));
        if (recipients.Count == 0)
            throw new AppException(
                "Configure al menos un destinatario en Configuración › Reportes por correo.");

        var day = date.Date;
        var lastSent = await _settings.GetAsync(SettingsKeys.DailyReportEmailLastSentDate);
        if (!force && string.Equals(lastSent, day.ToString("yyyy-MM-dd"), StringComparison.Ordinal))
        {
            return new DailyReportEmailResultDto(false, true,
                $"El cierre del {day:dd/MM/yyyy} ya fue enviado. Use «Reenviar» para enviarlo otra vez.",
                null, string.Join(", ", recipients));
        }

        var closing = await _reports.DailyClosingAsync(day);
        var config = await LoadConfigAsync();
        var subject = $"Cierre diario · {closing.BusinessName} · {day:dd/MM/yyyy}";
        var body = BuildHtml(closing);
        var csv = BuildCsv(closing);

        await SendAsync(config, recipients, subject, body,
            ($"cierre-{day:yyyyMMdd}.csv", Encoding.UTF8.GetBytes(csv)));

        await _settings.SetAsync(SettingsKeys.DailyReportEmailLastSentDate, day.ToString("yyyy-MM-dd"));

        return new DailyReportEmailResultDto(true, false,
            $"Cierre del {day:dd/MM/yyyy} enviado a {string.Join(", ", recipients)}.",
            DateTime.UtcNow, string.Join(", ", recipients));
    }

    /// <summary>
    /// Envía un correo de prueba. Usa los datos SMTP del formulario cuando se
    /// reciben; si faltan, completa con la configuración guardada.
    /// </summary>
    public async Task<DailyReportEmailResultDto> SendTestAsync(TestEmailRequest? request)
    {
        var recipients = string.IsNullOrWhiteSpace(request?.Recipient)
            ? ParseRecipients(await _settings.GetAsync(SettingsKeys.DailyReportEmailRecipients))
            : ParseRecipients(request.Recipient);

        if (recipients.Count == 0)
            throw new AppException("Indique un destinatario o configure los destinatarios del cierre diario.");

        var config = await LoadConfigAsync(request);
        var body = """
            <div style="font-family:Segoe UI,Arial,sans-serif;color:#1f2937">
              <h2 style="color:#0e7490">Correo de prueba · CyberManager</h2>
              <p>La configuración SMTP funciona correctamente.</p>
              <p style="color:#6b7280;font-size:12px">Este mensaje fue enviado desde la consola de administración.</p>
            </div>
            """;

        await SendAsync(config, recipients, "Prueba de correo · CyberManager", body, null);

        return new DailyReportEmailResultDto(true, false,
            $"Correo de prueba enviado a {string.Join(", ", recipients)}.",
            DateTime.UtcNow, string.Join(", ", recipients));
    }

    // ============================================================
    //  Configuración y envío
    // ============================================================
    private async Task<SmtpConfig> LoadConfigAsync(TestEmailRequest? overrides = null)
    {
        var host = !string.IsNullOrWhiteSpace(overrides?.SmtpHost)
            ? overrides!.SmtpHost!.Trim()
            : (await _settings.GetAsync(SettingsKeys.SmtpHost)).Trim();
        var port = overrides?.SmtpPort ?? await _settings.GetIntAsync(SettingsKeys.SmtpPort, 587);
        var useSsl = overrides?.SmtpUseSsl ?? await _settings.GetBoolAsync(SettingsKeys.SmtpUseSsl, true);
        var username = !string.IsNullOrWhiteSpace(overrides?.SmtpUsername)
            ? overrides!.SmtpUsername!.Trim()
            : (await _settings.GetAsync(SettingsKeys.SmtpUsername)).Trim();
        // Una contraseña vacía en el formulario conserva la guardada en el servidor.
        var password = !string.IsNullOrEmpty(overrides?.SmtpPassword)
            ? overrides.SmtpPassword!
            : await _settings.GetAsync(SettingsKeys.SmtpPassword);
        var from = !string.IsNullOrWhiteSpace(overrides?.SmtpFromAddress)
            ? overrides!.SmtpFromAddress!.Trim()
            : (await _settings.GetAsync(SettingsKeys.SmtpFromAddress)).Trim();
        var fromName = !string.IsNullOrWhiteSpace(overrides?.SmtpFromName)
            ? overrides!.SmtpFromName!.Trim()
            : (await _settings.GetAsync(SettingsKeys.SmtpFromName, "CyberManager")).Trim();

        if (string.IsNullOrWhiteSpace(host))
            throw new AppException(
                "Configure el servidor SMTP en Configuración › Reportes por correo.");

        if (port is < 1 or > 65535)
            throw new AppException("El puerto SMTP debe estar entre 1 y 65535.");

        if (string.IsNullOrWhiteSpace(from) && username.Contains('@'))
            from = username;

        if (string.IsNullOrWhiteSpace(from) || !IsEmail(from))
            throw new AppException("Configure una dirección de remitente válida en la configuración SMTP.");

        return new SmtpConfig(host, port, useSsl, username, password,
            from, string.IsNullOrWhiteSpace(fromName) ? "CyberManager" : fromName);
    }

    private static async Task SendAsync(SmtpConfig config, List<string> recipients,
        string subject, string htmlBody, (string Name, byte[] Data)? attachment)
    {
        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(config.From, config.FromName),
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                Body = htmlBody,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };

            foreach (var recipient in recipients) message.To.Add(new MailAddress(recipient));

            if (attachment is { } file)
            {
                var stream = new MemoryStream(file.Data);
                message.Attachments.Add(new Attachment(stream, file.Name, "text/csv"));
            }

            using var client = new SmtpClient(config.Host, config.Port)
            {
                EnableSsl = config.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Timeout = 20000
            };

            if (!string.IsNullOrWhiteSpace(config.Username))
                client.Credentials = new NetworkCredential(config.Username, config.Password);

            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            throw new AppException($"No se pudo enviar el correo: {ex.Message}", 502);
        }
    }

    private static List<string> ParseRecipients(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new List<string>();

        return value
            .Split(new[] { ',', ';', ' ', '\r', '\n', '\t' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsEmail)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            var address = new MailAddress(value.Trim());
            return address.Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private sealed record SmtpConfig(string Host, int Port, bool UseSsl, string Username,
        string Password, string From, string FromName);

    // ============================================================
    //  Contenido del correo
    // ============================================================
    private static string BuildHtml(DailyClosingDto c)
    {
        static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
        static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        var html = new StringBuilder();
        html.Append("""
            <div style="font-family:Segoe UI,Arial,sans-serif;color:#1f2937;max-width:720px">
              <div style="background:#0b1526;color:#e8effa;padding:18px 22px;border-radius:10px 10px 0 0">
                <div style="font-size:20px;font-weight:700">Cierre diario</div>
                <div style="color:#8fa4c4;font-size:13px;margin-top:4px">
            """);
        html.Append(E(c.BusinessName)).Append(" · ").Append(c.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        html.Append(" · Tasa 1 USD = ").Append(Money(c.ExchangeRate)).Append(" Bs</div></div>");

        html.Append("""
            <div style="border:1px solid #d7dee8;border-top:0;padding:18px 22px;border-radius:0 0 10px 10px">
              <table style="width:100%;border-collapse:collapse;font-size:14px">
            """);

        void Row(string label, string value, bool strong = false)
        {
            var weight = strong ? "700" : "400";
            var size = strong ? "16px" : "14px";
            html.Append("<tr><td style=\"padding:5px 0;color:#5b6b80\">").Append(E(label))
                .Append("</td><td style=\"padding:5px 0;text-align:right;font-weight:").Append(weight)
                .Append(";font-size:").Append(size).Append("\">").Append(E(value)).Append("</td></tr>");
        }

        Row("Ventas de productos", $"$ {Money(c.ProductSales)}");
        Row("Ventas de tiempo", $"$ {Money(c.TimeSales)}");
        Row("Total USD", $"$ {Money(c.TotalSales)}", true);
        Row("Total Bs", $"Bs {Money(c.TotalSalesVes)}");
        Row("IGTF", $"$ {Money(c.Igtf)}");
        Row("Descuentos", $"$ {Money(c.Discounts)}");
        Row("Ticket promedio", $"$ {Money(c.AverageTicket)}");
        Row("Ventas registradas", c.SaleCount.ToString(CultureInfo.InvariantCulture));
        Row("Sesiones", $"{c.SessionCount} · {c.SessionMinutes} min");

        html.Append("""
              </table>
              <h3 style="color:#0e7490;margin:18px 0 6px;font-size:15px">Caja y gastos</h3>
              <table style="width:100%;border-collapse:collapse;font-size:14px">
            """);
        Row("Fondo inicial", $"$ {Money(c.CashOpening)}");
        Row("Efectivo esperado", $"$ {Money(c.CashExpected)}");
        Row("Efectivo contado", c.CashCounted is decimal counted ? $"$ {Money(counted)}" : "—");
        Row("Diferencia", $"$ {Money(c.CashDifference)}");
        Row("Gastos de caja", $"$ {Money(c.CashExpenses)}");
        Row("Pagos a proveedores", $"$ {Money(c.SupplierPayments)}");
        Row("Total gastos", $"$ {Money(c.TotalExpenses)}");
        html.Append("</table>");

        if (c.Payments.Count > 0)
        {
            html.Append("""
                  <h3 style="color:#0e7490;margin:18px 0 6px;font-size:15px">Pagos del día</h3>
                  <table style="width:100%;border-collapse:collapse;font-size:14px">
                """);
            foreach (var row in c.Payments)
                Row($"{PaymentName(row.Method)} ({row.Count})", $"$ {Money(row.Total)}");
            html.Append("</table>");
        }

        if (c.TopProducts.Count > 0)
        {
            html.Append("""
                  <h3 style="color:#0e7490;margin:18px 0 6px;font-size:15px">Productos más vendidos</h3>
                  <table style="width:100%;border-collapse:collapse;font-size:14px">
                """);
            foreach (var product in c.TopProducts.Take(10))
                Row($"{product.Quantity:0.##} × {product.Name}", $"$ {Money(product.Total)}");
            html.Append("</table>");
        }

        html.Append("""
              <p style="color:#6b7280;font-size:12px;margin-top:18px">
                Se adjunta el detalle en formato CSV. Reporte generado automáticamente por CyberManager.
              </p>
            </div></div>
            """);

        return html.ToString();
    }

    private static string BuildCsv(DailyClosingDto c)
    {
        static string Q(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
        static string N(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        var csv = new StringBuilder();
        csv.AppendLine("Concepto;Valor");
        csv.AppendLine($"{Q("Negocio")};{Q(c.BusinessName)}");
        csv.AppendLine($"{Q("RIF")};{Q(c.BusinessTaxId)}");
        csv.AppendLine($"{Q("Fecha")};{Q(c.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))}");
        csv.AppendLine($"{Q("Tasa Bs/USD")};{N(c.ExchangeRate)}");
        csv.AppendLine($"{Q("Ventas de productos USD")};{N(c.ProductSales)}");
        csv.AppendLine($"{Q("Ventas de tiempo USD")};{N(c.TimeSales)}");
        csv.AppendLine($"{Q("Total USD")};{N(c.TotalSales)}");
        csv.AppendLine($"{Q("Total Bs")};{N(c.TotalSalesVes)}");
        csv.AppendLine($"{Q("IGTF USD")};{N(c.Igtf)}");
        csv.AppendLine($"{Q("Descuentos USD")};{N(c.Discounts)}");
        csv.AppendLine($"{Q("Ticket promedio USD")};{N(c.AverageTicket)}");
        csv.AppendLine($"{Q("Ventas registradas")};{c.SaleCount}");
        csv.AppendLine($"{Q("Sesiones")};{c.SessionCount}");
        csv.AppendLine($"{Q("Minutos vendidos")};{c.SessionMinutes}");
        csv.AppendLine($"{Q("Fondo inicial USD")};{N(c.CashOpening)}");
        csv.AppendLine($"{Q("Efectivo esperado USD")};{N(c.CashExpected)}");
        csv.AppendLine($"{Q("Efectivo contado USD")};{(c.CashCounted is decimal counted ? N(counted) : string.Empty)}");
        csv.AppendLine($"{Q("Diferencia USD")};{N(c.CashDifference)}");
        csv.AppendLine($"{Q("Gastos de caja USD")};{N(c.CashExpenses)}");
        csv.AppendLine($"{Q("Pagos a proveedores USD")};{N(c.SupplierPayments)}");
        csv.AppendLine($"{Q("Total gastos USD")};{N(c.TotalExpenses)}");

        csv.AppendLine();
        csv.AppendLine("Forma de pago;Cantidad;Total USD;Total Bs");
        foreach (var row in c.Payments)
            csv.AppendLine($"{Q(PaymentName(row.Method))};{row.Count};{N(row.Total)};{N(row.TotalVes)}");

        csv.AppendLine();
        csv.AppendLine("Producto;Cantidad;Total USD;Total Bs");
        foreach (var product in c.TopProducts)
            csv.AppendLine($"{Q(product.Name)};{product.Quantity.ToString("0.##", CultureInfo.InvariantCulture)};{N(product.Total)};{N(product.TotalVes)}");

        // BOM para que Excel reconozca los acentos y el separador punto y coma.
        return "\uFEFF" + csv;
    }

    private static string PaymentName(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Efectivo en bolívares",
        PaymentMethod.CashUsd => "Efectivo en dólares",
        PaymentMethod.Card => "Punto de venta / tarjeta",
        PaymentMethod.Transfer => "Transferencia",
        PaymentMethod.CustomerBalance => "Saldo del cliente",
        PaymentMethod.Mixed => "Pago mixto",
        PaymentMethod.Pending => "Pendiente por cobrar",
        PaymentMethod.PagoMovil => "Pago Móvil",
        _ => method.ToString()
    };
}
