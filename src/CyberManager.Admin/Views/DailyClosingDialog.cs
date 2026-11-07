using System.Windows;
using CyberManager.Admin.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

/// <summary>Resumen imprimible del cierre diario: ventas, caja, pagos y proveedores.</summary>
public sealed class DailyClosingDialog : FormDialog
{
    public DailyClosingDialog(DailyClosingDto closing, TicketSettings settings)
        : base($"Cierre diario · {closing.Date:dd/MM/yyyy}",
            $"{closing.BusinessName} · Tasa 1 USD = {closing.ExchangeRate:N2} Bs")
    {
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

        AddNote(closing.CashCounted is null
            ? "La caja del día todavía no tiene un cierre registrado."
            : "El contado corresponde al cierre de caja del turno.");
    }
}
