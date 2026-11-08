using System.Windows;
using System.Windows.Controls;
using CyberManager.Admin.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

/// <summary>Resumen imprimible del cierre diario: ventas, caja, pagos y proveedores.</summary>
public sealed class DailyClosingDialog : FormDialog
{
    private readonly DateTime _date;
    private readonly TextBlock _emailStatus;
    private readonly Button _emailButton;
    private bool _emailBusy;

    public DailyClosingDialog(DailyClosingDto closing, TicketSettings settings, bool emailEnabled = false)
        : base($"Cierre diario · {closing.Date:dd/MM/yyyy}",
            $"{closing.BusinessName} · Tasa 1 USD = {closing.ExchangeRate:N2} Bs")
    {
        _date = closing.Date.Date;
        AcceptButton.Content = "Cerrar";
        HideCancel();

        AddRow("Ventas de productos", $"$ {closing.ProductSales:N2}");
        AddRow("Ventas de tiempo", $"$ {closing.TimeSales:N2}");
        AddRow("Total USD", $"$ {closing.TotalSales:N2}", highlight: true);
        AddRow("Total Bs", $"Bs {closing.TotalSalesVes:N2}");
        AddRow("IGTF", $"$ {closing.Igtf:N2}");
        AddRow("Descuentos", $"$ {closing.Discounts:N2}");
        AddRow("Ticket promedio", $"$ {closing.AverageTicket:N2}");
        AddRow("Ventas registradas", closing.SaleCount.ToString());
        AddRow("Sesiones", $"{closing.SessionCount} · {closing.SessionMinutes:N0} min");

        AddSeparator();
        AddRow("Fondo inicial", $"$ {closing.CashOpening:N2}");
        AddRow("Efectivo esperado", $"$ {closing.CashExpected:N2}");
        AddRow("Efectivo contado", closing.CashCounted is decimal counted ? $"$ {counted:N2}" : "—");
        AddRow("Diferencia", $"$ {closing.CashDifference:N2}");
        AddRow("Gastos de caja", $"$ {closing.CashExpenses:N2}");
        AddRow("Pagos a proveedores", $"$ {closing.SupplierPayments:N2}");
        AddRow("Total gastos", $"$ {closing.TotalExpenses:N2}");

        if (closing.Payments.Count > 0)
        {
            AddSeparator();
            foreach (var row in closing.Payments)
                AddRow($"{Labels.PaymentName(row.Method)} ({row.Count})", $"$ {row.Total:N2}");
        }

        if (closing.SupplierPaymentsDetail.Count > 0)
        {
            AddSeparator();
            foreach (var payment in closing.SupplierPaymentsDetail)
                AddRow($"{payment.SupplierName} · {Labels.PaymentName(payment.Method)}",
                    $"$ {payment.Amount:N2}");
        }

        if (closing.TopProducts.Count > 0)
        {
            AddSeparator();
            foreach (var product in closing.TopProducts.Take(5))
                AddRow($"{product.Quantity:0.##} × {product.Name}", $"$ {product.Total:N2}");
        }

        var print = Kit.Button("Imprimir ticket", "BlueButton", () =>
        {
            try
            {
                if (!TicketPrinter.PrintClosing(closing, settings))
                    Ui.Info("No se seleccionó una impresora.", "Impresión cancelada");
            }
            catch (Exception ex)
            {
                Ui.Error(ex.Message, "No se pudo imprimir el cierre");
            }
        });
        print.HorizontalAlignment = HorizontalAlignment.Left;
        print.Margin = new Thickness(0, 12, 0, 0);
        Body.Children.Add(print);

        _emailButton = Kit.Button("Enviar por correo", "GhostButton", () => _ = SendEmailAsync(force: true));
        _emailButton.HorizontalAlignment = HorizontalAlignment.Left;
        _emailButton.Margin = new Thickness(0, 8, 0, 0);
        Body.Children.Add(_emailButton);

        _emailStatus = new TextBlock
        {
            Text = emailEnabled
                ? "Preparando el envío del cierre por correo..."
                : "El envío automático por correo está desactivado en Configuración.",
            Style = Style("Muted"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        Body.Children.Add(_emailStatus);

        AddNote(closing.CashCounted is null
            ? "La caja del día todavía no tiene un cierre registrado."
            : "El contado corresponde al cierre de caja del turno.");

        if (emailEnabled)
            Loaded += async (_, _) => await SendEmailAsync(force: false);
    }

    private async Task SendEmailAsync(bool force)
    {
        if (_emailBusy) return;
        _emailBusy = true;
        _emailButton.IsEnabled = false;
        _emailStatus.Text = "Enviando el cierre por correo...";
        _emailStatus.Foreground = Brush("MutedBrush");

        try
        {
            var result = await App.Api.SendDailyClosingEmailAsync(_date, force);
            _emailStatus.Text = result.Message;
            _emailStatus.Foreground = Brush(result.Sent ? "SuccessBrush" : "MutedBrush");
            if (result.Sent) _emailButton.Content = "Reenviar por correo";
        }
        catch (Exception ex)
        {
            _emailStatus.Text = ex.Message;
            _emailStatus.Foreground = Brush("DangerBrush");
        }
        finally
        {
            _emailBusy = false;
            _emailButton.IsEnabled = true;
        }
    }
}
