using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CyberManager.Admin.Services;
using CyberManager.Shared.Dtos;
using Microsoft.Win32;

namespace CyberManager.Admin.Views;

/// <summary>Reportes de ventas, productos, equipos, inventario y cajas con exportación CSV.</summary>
public sealed class ReportsView : UserControl, IRefreshable
{
    private readonly DatePicker _fromPicker = new() { SelectedDate = DateTime.Today.AddDays(-6) };
    private readonly DatePicker _toPicker = new() { SelectedDate = DateTime.Today };
    private readonly TextBlock _rangeText = Kit.Muted("Consultando...");
    private readonly TextBlock _totalValue;
    private readonly TextBlock _totalBsValue;
    private readonly TextBlock _productsValue;
    private readonly TextBlock _timeValue;
    private readonly TextBlock _ticketValue;
    private readonly TextBlock _discountsValue;
    private readonly TextBlock _sessionsValue;
    private readonly TextBlock _minutesValue;

    private readonly TabControl _tabs = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly DataGrid _salesGrid;
    private readonly DataGrid _productsGrid;
    private readonly DataGrid _stationsGrid;
    private readonly DataGrid _inventoryGrid;
    private readonly DataGrid _cashGrid;

    private ReportSummaryDto? _summary;
    private List<SalesReportRow> _sales = new();
    private List<ProductReportRow> _products = new();
    private List<StationReportRow> _stations = new();
    private List<InventoryReportRow> _inventory = new();
    private List<CashReportRow> _cash = new();

    public ReportsView()
    {
        _salesGrid = Kit.Table(
            Kit.Col("Fecha", nameof(SalesReportRow.Date), 115, "{0:dd/MM/yyyy}"),
            Kit.Col("Ventas", nameof(SalesReportRow.Count), 80),
            Kit.Col("Productos USD", nameof(SalesReportRow.Products), 115, "{0:N2}"),
            Kit.Col("Tiempo USD", nameof(SalesReportRow.Time), 110, "{0:N2}"),
            Kit.Col("Total USD", nameof(SalesReportRow.Total), 110, "{0:N2}"),
            Kit.Col("Productos Bs", nameof(SalesReportRow.ProductsVes), 110, "{0:N2}"),
            Kit.Col("Tiempo Bs", nameof(SalesReportRow.TimeVes), 110, "{0:N2}"),
            Kit.Col("Total Bs", nameof(SalesReportRow.TotalVes), 120, "{0:N2}"));

        _productsGrid = Kit.Table(
            Kit.Col("Producto", nameof(ProductReportRow.Name)),
            Kit.Col("Cantidad", nameof(ProductReportRow.Quantity), 110, "{0:N2}"),
            Kit.Col("Total USD", nameof(ProductReportRow.Total), 120, "{0:N2}"),
            Kit.Col("Total Bs", nameof(ProductReportRow.TotalVes), 130, "{0:N2}"));

        _stationsGrid = Kit.Table(
            Kit.Col("Equipo", nameof(StationReportRow.Name)),
            Kit.Col("Sesiones", nameof(StationReportRow.Sessions), 100),
            Kit.Col("Minutos", nameof(StationReportRow.Minutes), 100),
            Kit.Col("Tiempo USD", nameof(StationReportRow.TimeAmount), 115, "{0:N2}"),
            Kit.Col("Consumos USD", nameof(StationReportRow.Consumption), 120, "{0:N2}"),
            Kit.Col("Tiempo Bs", nameof(StationReportRow.TimeAmountVes), 120, "{0:N2}"),
            Kit.Col("Consumos Bs", nameof(StationReportRow.ConsumptionVes), 125, "{0:N2}"));

        _inventoryGrid = Kit.Table(
            Kit.Col("SKU", nameof(InventoryReportRow.Sku), 120),
            Kit.Col("Producto", nameof(InventoryReportRow.Name)),
            Kit.Col("Stock", nameof(InventoryReportRow.Stock), 100, "{0:N2}"),
            Kit.Col("Mínimo", nameof(InventoryReportRow.MinStock), 100, "{0:N2}"),
            Kit.Col("Costo", nameof(InventoryReportRow.Cost), 100, "{0:N2}"),
            Kit.Col("Precio", nameof(InventoryReportRow.Price), 100, "{0:N2}"));

        _cashGrid = Kit.Table(
            Kit.Col("Apertura", nameof(CashRow.Opened), 140),
            Kit.Col("Cierre", nameof(CashRow.Closed), 140),
            Kit.Col("Usuario", nameof(CashRow.User), 140),
            Kit.Col("Fondo", nameof(CashRow.Opening), 100, "{0:N2}"),
            Kit.Col("Ventas", nameof(CashRow.Sales), 100, "{0:N2}"),
            Kit.Col("Gastos", nameof(CashRow.Expenses), 100, "{0:N2}"),
            Kit.Col("Esperado", nameof(CashRow.Expected), 105, "{0:N2}"),
            Kit.Col("Contado", nameof(CashRow.Closing), 100, "{0:N2}"),
            Kit.Col("Diferencia", nameof(CashRow.Difference), 105, "{0:N2}"));

        _tabs.Items.Add(new TabItem { Header = "Ventas por día", Content = _salesGrid });
        _tabs.Items.Add(new TabItem { Header = "Productos", Content = _productsGrid });
        _tabs.Items.Add(new TabItem { Header = "Equipos", Content = _stationsGrid });
        _tabs.Items.Add(new TabItem { Header = "Inventario valorizado", Content = _inventoryGrid });
        _tabs.Items.Add(new TabItem { Header = "Cajas", Content = _cashGrid });
        foreach (TabItem tab in _tabs.Items) tab.Padding = new Thickness(14, 8, 14, 8);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var range = Kit.HStack(8,
            Kit.Label("Desde"), _fromPicker,
            Kit.Muted("hasta"), _toPicker,
            Kit.Button("Consultar", "BlueButton", () => _ = Ui.Run(RefreshAsync, "No se pudieron cargar los reportes")),
            Kit.Button("Exportar CSV", "GhostButton", ExportCsv),
            Kit.Button("Cierre diario", "SuccessButton",
                () => _ = Ui.Run(ShowDailyClosingAsync, "No se pudo cargar el cierre diario")));
        range.VerticalAlignment = VerticalAlignment.Center;

        var headerTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        _rangeText.HorizontalAlignment = HorizontalAlignment.Right;
        headerTexts.Children.Add(_rangeText);
        headerTexts.Children.Add(Kit.Muted("Los importes en Bs usan la tasa registrada en cada venta."));

        root.Children.Add(Kit.PageHeader("Reportes y estadísticas",
            "Analice ventas, consumo por producto, rendimiento por equipo y arqueos de caja.",
            range, headerTexts));

        var metrics = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 16) };
        metrics.Children.Add(Metric("Ventas totales USD", "SuccessBrush", out _totalValue));
        metrics.Children.Add(Metric("Ventas totales Bs", "PurpleBrush", out _totalBsValue));
        metrics.Children.Add(Metric("Productos USD", "AccentBrush", out _productsValue));
        metrics.Children.Add(Metric("Tiempo USD", "Accent2Brush", out _timeValue));
        metrics.Children.Add(Metric("Ticket promedio", "WarningBrush", out _ticketValue));
        metrics.Children.Add(Metric("Descuentos", "DangerBrush", out _discountsValue));
        metrics.Children.Add(Metric("Sesiones", "Accent2Brush", out _sessionsValue));
        metrics.Children.Add(Metric("Minutos vendidos", "AccentBrush", out _minutesValue));
        Grid.SetRow(metrics, 1);
        root.Children.Add(metrics);

        Grid.SetRow(_tabs, 2);
        root.Children.Add(_tabs);

        Content = root;

        Loaded += async (_, _) => await Ui.Run(RefreshAsync, "No se pudieron cargar los reportes");
    }

    private static Border Metric(string label, string accent, out TextBlock valueText)
    {
        var card = Kit.StatCard(label, "—", null, accent, out var value);
        card.Margin = new Thickness(0, 0, 10, 10);
        valueText = value;
        return card;
    }

    public async Task RefreshAsync()
    {
        var from = (_fromPicker.SelectedDate ?? DateTime.Today.AddDays(-6)).Date;
        var to = (_toPicker.SelectedDate ?? DateTime.Today).Date;

        if (to < from)
        {
            Ui.Error("La fecha final no puede ser anterior a la inicial.", "Rango de fechas");
            return;
        }

        var summaryTask = App.Api.ReportSummaryAsync(from, to);
        var salesTask = App.Api.ReportSalesAsync(from, to);
        var productsTask = App.Api.ReportProductsAsync(from, to);
        var stationsTask = App.Api.ReportStationsAsync(from, to);
        var inventoryTask = App.Api.ReportInventoryAsync();
        var cashTask = App.Api.CashHistoryAsync(from, to);

        await Task.WhenAll(summaryTask, salesTask, productsTask, stationsTask, inventoryTask, cashTask);

        _summary = await summaryTask;
        _sales = await salesTask;
        _products = await productsTask;
        _stations = await stationsTask;
        _inventory = await inventoryTask;
        _cash = await cashTask;

        Render();
    }

    private void Render()
    {
        if (_summary is not { } summary) return;

        _totalValue.Text = Ui.Money(summary.TotalSales);
        _totalBsValue.Text = Ui.MoneyBs(summary.TotalSalesVes);
        _productsValue.Text = Ui.Money(summary.ProductSales);
        _timeValue.Text = Ui.Money(summary.TimeSales);
        _ticketValue.Text = Ui.Money(summary.AverageTicket);
        _discountsValue.Text = Ui.Money(summary.Discounts);
        _sessionsValue.Text = summary.SessionCount.ToString();
        _minutesValue.Text = $"{summary.SessionMinutes:N0} min";

        _totalValue.ToolTip = $"{summary.SaleCount} ventas registradas";
        _totalBsValue.ToolTip = summary.ExchangeRate > 0
            ? $"Tasa de referencia: 1 USD = {summary.ExchangeRate:N2} Bs"
            : "Sin tasa activa";
        _sessionsValue.ToolTip = $"{summary.SessionMinutes:N0} minutos de tiempo vendidos";

        var from = (_fromPicker.SelectedDate ?? DateTime.Today).Date;
        var to = (_toPicker.SelectedDate ?? DateTime.Today).Date;
        _rangeText.Text = $"{from:dd/MM/yyyy} al {to:dd/MM/yyyy} · {summary.SaleCount} ventas · " +
                          $"{summary.SessionCount} sesiones";

        _salesGrid.ItemsSource = _sales;
        _productsGrid.ItemsSource = _products;
        _stationsGrid.ItemsSource = _stations;
        _inventoryGrid.ItemsSource = _inventory;
        _cashGrid.ItemsSource = _cash.Select(c => new CashRow(c)).ToList();
    }

    private async Task ShowDailyClosingAsync()
    {
        var date = (_toPicker.SelectedDate ?? DateTime.Today).Date;
        var closingTask = App.Api.DailyClosingAsync(date);
        var settingsTask = TicketSettings.LoadAsync();
        await Task.WhenAll(closingTask, settingsTask);

        var dialog = new DailyClosingDialog(await closingTask, await settingsTask)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    private void ExportCsv()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exportar reporte",
            Filter = "Archivo CSV (*.csv)|*.csv",
            FileName = $"reporte-{_tabs.SelectedIndex switch { 0 => "ventas", 1 => "productos", 2 => "equipos", 3 => "inventario", _ => "cajas" }}-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var (headers, rows) = BuildCsv();
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(';', headers.Select(Escape)));

            foreach (var row in rows)
                builder.AppendLine(string.Join(';', row.Select(Escape)));

            File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
            Ui.Info($"Reporte exportado en:\n{dialog.FileName}", "Exportación completada");
        }
        catch (Exception ex)
        {
            Ui.Error(ex.Message, "No se pudo exportar el reporte");
        }
    }

    private (string[] Headers, List<string[]> Rows) BuildCsv()
    {
        switch (_tabs.SelectedIndex)
        {
            case 1:
                return (
                    new[] { "Producto", "Cantidad", "Total USD", "Total Bs" },
                    _products.Select(p => new[]
                    {
                        p.Name, p.Quantity.ToString("N2", CultureInfo.CurrentCulture),
                        p.Total.ToString("N2", CultureInfo.CurrentCulture),
                        p.TotalVes.ToString("N2", CultureInfo.CurrentCulture)
                    }).ToList());

            case 2:
                return (
                    new[] { "Equipo", "Sesiones", "Minutos", "Tiempo USD", "Consumos USD", "Tiempo Bs", "Consumos Bs" },
                    _stations.Select(s => new[]
                    {
                        s.Name, s.Sessions.ToString(), s.Minutes.ToString(),
                        s.TimeAmount.ToString("N2", CultureInfo.CurrentCulture),
                        s.Consumption.ToString("N2", CultureInfo.CurrentCulture),
                        s.TimeAmountVes.ToString("N2", CultureInfo.CurrentCulture),
                        s.ConsumptionVes.ToString("N2", CultureInfo.CurrentCulture)
                    }).ToList());

            case 3:
                return (
                    new[] { "SKU", "Producto", "Stock", "Mínimo", "Costo", "Precio" },
                    _inventory.Select(p => new[]
                    {
                        p.Sku, p.Name,
                        p.Stock.ToString("N2", CultureInfo.CurrentCulture),
                        p.MinStock.ToString("N2", CultureInfo.CurrentCulture),
                        p.Cost.ToString("N2", CultureInfo.CurrentCulture),
                        p.Price.ToString("N2", CultureInfo.CurrentCulture)
                    }).ToList());

            case 4:
                return (
                    new[] { "Apertura", "Cierre", "Usuario", "Fondo", "Ventas", "Gastos", "Esperado", "Contado", "Diferencia" },
                    _cash.Select(c => new[]
                    {
                        c.OpenedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                        c.ClosedUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Abierta",
                        c.UserName ?? "",
                        c.OpeningAmount.ToString("N2", CultureInfo.CurrentCulture),
                        c.Sales.ToString("N2", CultureInfo.CurrentCulture),
                        c.Expenses.ToString("N2", CultureInfo.CurrentCulture),
                        c.Expected.ToString("N2", CultureInfo.CurrentCulture),
                        (c.Closing ?? 0m).ToString("N2", CultureInfo.CurrentCulture),
                        c.Difference.ToString("N2", CultureInfo.CurrentCulture)
                    }).ToList());

            default:
                return (
                    new[] { "Fecha", "Ventas", "Productos USD", "Tiempo USD", "Total USD", "Productos Bs", "Tiempo Bs", "Total Bs" },
                    _sales.Select(s => new[]
                    {
                        s.Date.ToString("dd/MM/yyyy"),
                        s.Count.ToString(),
                        s.Products.ToString("N2", CultureInfo.CurrentCulture),
                        s.Time.ToString("N2", CultureInfo.CurrentCulture),
                        s.Total.ToString("N2", CultureInfo.CurrentCulture),
                        s.ProductsVes.ToString("N2", CultureInfo.CurrentCulture),
                        s.TimeVes.ToString("N2", CultureInfo.CurrentCulture),
                        s.TotalVes.ToString("N2", CultureInfo.CurrentCulture)
                    }).ToList());
        }
    }

    private static string Escape(string value)
    {
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

public sealed class CashRow
{
    public CashRow(CashReportRow row)
    {
        Opened = row.OpenedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        Closed = row.ClosedUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Abierta";
        User = row.UserName ?? "—";
        Opening = row.OpeningAmount;
        Sales = row.Sales;
        Expenses = row.Expenses;
        Expected = row.Expected;
        Closing = row.Closing ?? 0m;
        Difference = row.Difference;
    }

    public string Opened { get; }
    public string Closed { get; }
    public string User { get; }
    public decimal Opening { get; }
    public decimal Sales { get; }
    public decimal Expenses { get; }
    public decimal Expected { get; }
    public decimal Closing { get; }
    public decimal Difference { get; }
}
