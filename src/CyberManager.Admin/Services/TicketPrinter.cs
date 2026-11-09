using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CyberManager.Admin.Views;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Services;

/// <summary>Datos de encabezado y configuración de impresión de tickets.</summary>
public sealed record TicketSettings(
    string BusinessName, string? TaxId, string? Address, string? Phone,
    string? Header, string? Footer, string? PrinterName, int Copies, bool AutoPrint,
    string ClosingFormat)
{
    public static TicketSettings From(IReadOnlyDictionary<string, string> settings)
    {
        string Get(string key, string fallback = "") =>
            settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

        var copies = int.TryParse(Get("TicketCopies", "1"), out var c) ? Math.Clamp(c, 1, 5) : 1;
        var autoPrint = bool.TryParse(Get("AutoPrintTicket", "false"), out var a) && a;

        return new TicketSettings(
            Get("BusinessName", "CyberManager"),
            Get("BusinessTaxId"),
            Get("BusinessAddress"),
            Get("BusinessPhone"),
            Get("TicketHeader"),
            Get("TicketFooter", "¡Gracias por su visita!"),
            Get("PrinterName"),
            copies,
            autoPrint,
            TicketPrinter.NormalizeClosingFormat(Get("DailyClosingFormat", "ticket80")));
    }

    public static async Task<TicketSettings> LoadAsync() => From(await App.Api.SettingsAsync());
}

/// <summary>Genera e imprime tickets de venta y cierres diarios con FlowDocument.</summary>
public static class TicketPrinter
{
    private const double TicketWidth = 302; // 80 mm a 96 ppp
    private const double A4Width = 794;      // 210 mm a 96 ppp
    private const double LetterWidth = 816;  // 216 mm a 96 ppp

    /// <summary>Normaliza el formato elegido para el cierre diario.</summary>
    public static string NormalizeClosingFormat(string? value) => (value ?? string.Empty)
        .Trim().ToLowerInvariant() switch
    {
        "a4" => "a4",
        "carta" or "letter" => "carta",
        _ => "ticket80"
    };

    public static bool PrintSale(SaleDto sale, TicketSettings settings, bool silent = false)
        => Print(BuildSale(sale, settings), silent ? settings.PrinterName : null, settings.Copies,
            $"Ticket {sale.Number}", silent);

    public static bool PrintClosing(DailyClosingDto closing, TicketSettings settings, bool silent = false)
        => Print(BuildClosing(closing, settings), silent ? settings.PrinterName : null,
            settings.Copies, $"Cierre {closing.Date:yyyyMMdd}", silent);

    public static FlowDocument BuildSale(SaleDto sale, TicketSettings settings)
    {
        var doc = NewDocument();
        AddHeader(doc, settings, Labels.InvoiceName(sale.InvoiceType).ToUpperInvariant());

        AddKV(doc, "N.º", sale.Number);
        if (!string.IsNullOrWhiteSpace(sale.ControlNumber)) AddKV(doc, "Control", sale.ControlNumber!);
        AddKV(doc, "Fecha", sale.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        AddKV(doc, "Pago", Labels.PaymentName(sale.Method));
        if (!string.IsNullOrWhiteSpace(sale.CustomerName)) AddKV(doc, "Cliente", sale.CustomerName!);

        AddDivider(doc);

        var items = NewTable(2, new GridLength(1, GridUnitType.Star), new GridLength(92));
        foreach (var item in sale.Items)
        {
            AddCells(items,
                $"{item.Quantity:0.##} × {item.Description}",
                $"$ {item.Total:N2}");
        }
        doc.Blocks.Add(items);

        AddDivider(doc);
        AddKV(doc, "Subtotal", $"$ {sale.Subtotal:N2}");
        if (sale.Discount > 0) AddKV(doc, "Descuento", $"- $ {sale.Discount:N2}");
        AddKV(doc, "IVA incluido", $"$ {sale.Tax:N2}");
        if (sale.IgtfAmount > 0) AddKV(doc, "IGTF", $"$ {sale.IgtfAmount:N2}");
        AddKV(doc, "TOTAL USD", $"$ {sale.Total:N2}", bold: true, size: 12);
        AddKV(doc, "TOTAL Bs", $"Bs {sale.TotalVes:N2}", bold: true);
        AddKV(doc, "Tasa", $"1 USD = {sale.ExchangeRate:N2} Bs");

        AddDivider(doc);
        AddKV(doc, "Recibido", sale.PaidCurrency == Currency.Usd
            ? $"$ {sale.PaidAmount:N2}"
            : $"Bs {sale.PaidAmount:N2}");
        if (sale.Change > 0)
            AddKV(doc, "Cambio", sale.PaidCurrency == Currency.Usd
                ? $"$ {sale.Change:N2}"
                : $"Bs {sale.Change:N2}");

        if (!string.IsNullOrWhiteSpace(sale.Notes))
        {
            AddDivider(doc);
            AddLine(doc, sale.Notes!, size: 9.5);
        }

        if (sale.Status == SaleStatus.Cancelled)
            AddLine(doc, "*** VENTA ANULADA ***", bold: true, align: TextAlignment.Center);

        AddFooter(doc, settings);
        return doc;
    }

    public static FlowDocument BuildClosing(DailyClosingDto closing, TicketSettings settings)
    {
        var doc = NewDocument(settings.ClosingFormat);
        AddHeader(doc, settings, "CIERRE DE CAJA");
        AddKV(doc, "Fecha", closing.Date.ToString("dd/MM/yyyy"));
        AddKV(doc, "Tasa", $"1 USD = {closing.ExchangeRate:N2} Bs");

        AddDivider(doc);
        AddKV(doc, "Ventas productos", $"$ {closing.ProductSales:N2}");
        AddKV(doc, "Ventas tiempo", $"$ {closing.TimeSales:N2}");
        AddKV(doc, "TOTAL USD", $"$ {closing.TotalSales:N2}", bold: true, size: 12);
        AddKV(doc, "TOTAL Bs", $"Bs {closing.TotalSalesVes:N2}", bold: true);
        AddKV(doc, "IGTF", $"$ {closing.Igtf:N2}");
        AddKV(doc, "Descuentos", $"$ {closing.Discounts:N2}");
        AddKV(doc, "Ticket promedio", $"$ {closing.AverageTicket:N2}");
        AddKV(doc, "Ventas", closing.SaleCount.ToString());
        AddKV(doc, "Sesiones", $"{closing.SessionCount} · {closing.SessionMinutes:N0} min");

        AddDivider(doc);
        AddSection(doc, "CAJA");
        AddKV(doc, "Fondo inicial", $"$ {closing.CashOpening:N2}");
        AddKV(doc, "Esperado", $"$ {closing.CashExpected:N2}");
        AddKV(doc, "Contado", closing.CashCounted is decimal counted ? $"$ {counted:N2}" : "—");
        AddKV(doc, "Diferencia", $"$ {closing.CashDifference:N2}");
        AddKV(doc, "Gastos de caja", $"$ {closing.CashExpenses:N2}");
        AddKV(doc, "Pagos proveedores", $"$ {closing.SupplierPayments:N2}");
        AddKV(doc, "Total gastos", $"$ {closing.TotalExpenses:N2}");

        if (closing.Payments.Count > 0)
        {
            AddDivider(doc);
            AddSection(doc, "FORMAS DE PAGO");
            var payments = NewTable(2, new GridLength(1, GridUnitType.Star), new GridLength(92));
            foreach (var row in closing.Payments)
            {
                AddCells(payments, $"{Labels.PaymentName(row.Method)} ({row.Count})", $"$ {row.Total:N2}");
            }
            doc.Blocks.Add(payments);
        }

        if (closing.SupplierPaymentsDetail.Count > 0)
        {
            AddDivider(doc);
            AddSection(doc, "PAGOS A PROVEEDORES");
            foreach (var payment in closing.SupplierPaymentsDetail)
            {
                AddKV(doc, payment.SupplierName,
                    $"{Labels.PaymentName(payment.Method)} · $ {payment.Amount:N2}");
            }
        }

        if (closing.TopProducts.Count > 0)
        {
            AddDivider(doc);
            AddSection(doc, "PRODUCTOS VENDIDOS");
            foreach (var product in closing.TopProducts)
            {
                AddKV(doc, $"{product.Quantity:0.##} × {product.Name}", $"$ {product.Total:N2}");
            }
        }

        AddFooter(doc, settings);
        return doc;
    }

    public static bool Print(FlowDocument document, string? printerName, int copies, string jobName,
        bool silent = false)
    {
        if (copies < 1) copies = 1;
        var dialog = new PrintDialog();

        if (!string.IsNullOrWhiteSpace(printerName))
        {
            var queue = FindQueue(printerName);
            if (queue is not null) dialog.PrintQueue = queue;
        }

        if (dialog.PrintQueue is null)
        {
            if (silent) return false;
            if (dialog.ShowDialog() != true) return false;
        }

        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        for (var i = 0; i < copies; i++)
            dialog.PrintDocument(paginator, $"{jobName} ({i + 1}/{copies})");

        return true;
    }

    private static PrintQueue? FindQueue(string name)
    {
        try
        {
            using var server = new LocalPrintServer();
            return server.GetPrintQueues(new[]
                {
                    EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections
                })
                .FirstOrDefault(q =>
                    string.Equals(q.FullName, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(q.Name, name, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    // ===================== construcción del documento =====================

    private static FlowDocument NewDocument(string? closingFormat = null)
    {
        var format = NormalizeClosingFormat(closingFormat);
        var (width, padding, fontSize) = format switch
        {
            "a4" => (A4Width, 36d, 11.5d),
            "carta" => (LetterWidth, 36d, 11.5d),
            _ => (TicketWidth, 8d, 10.5d)
        };

        return new FlowDocument
        {
            PageWidth = width,
            ColumnWidth = width,
            PagePadding = new Thickness(padding),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = fontSize,
            Foreground = Brushes.Black,
            Background = Brushes.White
        };
    }

    private static void AddHeader(FlowDocument doc, TicketSettings settings, string title)
    {
        if (!string.IsNullOrWhiteSpace(settings.Header))
            AddLine(doc, settings.Header!, bold: true, align: TextAlignment.Center, size: 11);

        AddLine(doc, settings.BusinessName, bold: true, align: TextAlignment.Center, size: 15);
        if (!string.IsNullOrWhiteSpace(settings.TaxId))
            AddLine(doc, $"RIF: {settings.TaxId}", align: TextAlignment.Center, size: 9.5);
        if (!string.IsNullOrWhiteSpace(settings.Address))
            AddLine(doc, settings.Address!, align: TextAlignment.Center, size: 9);
        if (!string.IsNullOrWhiteSpace(settings.Phone))
            AddLine(doc, $"Tel.: {settings.Phone}", align: TextAlignment.Center, size: 9);

        AddDivider(doc);
        AddLine(doc, title, bold: true, align: TextAlignment.Center, size: 11.5);
        AddDivider(doc);
    }

    private static void AddFooter(FlowDocument doc, TicketSettings settings)
    {
        AddDivider(doc);
        AddLine(doc, settings.Footer ?? "¡Gracias por su visita!", align: TextAlignment.Center, size: 10);
        AddLine(doc, $"Impreso {DateTime.Now:dd/MM/yyyy HH:mm}", align: TextAlignment.Center, size: 8.5);
        AddLine(doc, "CyberManager", align: TextAlignment.Center, size: 8.5);
    }

    private static void AddSection(FlowDocument doc, string text)
        => AddLine(doc, text, bold: true, size: 10.5);

    private static void AddDivider(FlowDocument doc)
        => AddLine(doc, new string('-', 46), align: TextAlignment.Center, size: 8);

    private static void AddLine(FlowDocument doc, string text, bool bold = false,
        double size = 10.5, TextAlignment align = TextAlignment.Left)
    {
        var paragraph = new Paragraph(new Run(text))
        {
            Margin = new Thickness(0, 1, 0, 1),
            FontSize = size,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            TextAlignment = align,
            Foreground = Brushes.Black
        };
        doc.Blocks.Add(paragraph);
    }

    private static void AddKV(FlowDocument doc, string label, string value,
        bool bold = false, double size = 10.5)
    {
        var table = NewTable(2, new GridLength(1, GridUnitType.Star), new GridLength(104));
        AddCells(table, label, value, bold, size);
        doc.Blocks.Add(table);
    }

    private static Table NewTable(int columns, params GridLength[] widths)
    {
        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0)
        };

        for (var i = 0; i < columns; i++)
        {
            table.Columns.Add(new TableColumn
            {
                Width = i < widths.Length ? widths[i] : new GridLength(1, GridUnitType.Star)
            });
        }

        table.RowGroups.Add(new TableRowGroup());
        return table;
    }

    private static void AddCells(Table table, string left, string right,
        bool bold = false, double size = 10.5)
    {
        var row = new TableRow();
        row.Cells.Add(Cell(left, bold, size, TextAlignment.Left));
        row.Cells.Add(Cell(right, bold, size, TextAlignment.Right));
        table.RowGroups[0].Rows.Add(row);
    }

    private static TableCell Cell(string text, bool bold, double size, TextAlignment align)
        => new(new Paragraph(new Run(text))
        {
            Margin = new Thickness(0),
            FontSize = size,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            TextAlignment = align,
            Foreground = Brushes.Black
        })
        {
            Padding = new Thickness(0, 1, 0, 1),
            BorderThickness = new Thickness(0)
        };
}
