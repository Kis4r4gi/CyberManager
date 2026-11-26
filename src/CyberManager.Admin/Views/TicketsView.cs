using System.Windows;
using System.Windows.Controls;
using CyberManager.Admin.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

/// <summary>
/// Busca tickets emitidos por número, cliente/RIF o producto. Permite ver el detalle y reimprimir
/// el comprobante sin tener que repetir la venta.
/// </summary>
public sealed class TicketsView : UserControl, IRefreshable
{
    private static readonly DateTime DefaultFrom = DateTime.Today.AddYears(-1);

    private readonly TextBox _number = Kit.Input(placeholder: "Ej. V-000123 o 123");
    private readonly TextBox _customer = Kit.Input(placeholder: "Nombre o RIF del cliente");
    private readonly TextBox _product = Kit.Input(placeholder: "Producto del ticket");
    private readonly DatePicker _from = new() { SelectedDate = DefaultFrom };
    private readonly DatePicker _to = new() { SelectedDate = DateTime.Today };
    private readonly ComboBox _status = Kit.Combo<SaleStatus?>(new List<Option<SaleStatus?>>
    {
        new(null, "Todos los estados"),
        new(SaleStatus.Completed, "Completados"),
        new(SaleStatus.Cancelled, "Anulados")
    });
    private readonly TextBlock _count = Kit.Muted("Consultando...");
    private readonly DataGrid _grid;

    private List<SaleDto> _sales = new();
    private TicketSettings? _settings;

    public TicketsView()
    {
        _number.Width = 120;
        _customer.Width = 150;
        _product.Width = 150;
        _status.Width = 130;
        _from.Width = 130;
        _to.Width = 130;

        _grid = Kit.Table(
            Kit.Col("N° ticket", nameof(SaleDto.Number), 105),
            Kit.Col("Fecha", nameof(SaleDto.CreatedUtc), 130, "{0:dd/MM/yyyy HH:mm}"),
            Kit.Col("Cliente", nameof(SaleDto.CustomerName), 150),
            Kit.Col("RIF", nameof(SaleDto.CustomerTaxId), 115),
            Kit.Col("Equipo", nameof(SaleDto.StationName), 110),
            Kit.Col("Pago", nameof(SaleDto.Method), 105),
            Kit.Col("Total USD", nameof(SaleDto.Total), 105, "{0:N2}"),
            Kit.Col("Total Bs", nameof(SaleDto.TotalVes), 110, "{0:N2}"),
            Kit.Col("Estado", nameof(SaleDto.Status), 90),
            Kit.ActionCol("Detalle", "Ver", OnView, width: 70),
            Kit.ActionCol("Ticket", "Imprimir", OnPrint, width: 95));
        _grid.IsReadOnly = true;
        _grid.SelectionMode = DataGridSelectionMode.Single;

        // PageHeader reserva la columna derecha para unas pocas acciones. Una barra de
        // filtros completa allí exprime el subtítulo hasta hacerlo enorme y empuja la
        // tabla fuera de la ventana; por eso los filtros van en su propia fila.
        var filters = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        filters.Children.Add(Filter("N°", _number));
        filters.Children.Add(Filter("Desde", _from));
        filters.Children.Add(Filter("Hasta", _to));
        filters.Children.Add(Filter("Cliente/RIF", _customer));
        filters.Children.Add(Filter("Producto", _product));
        filters.Children.Add(Filter("Estado", _status));
        var searchButton = Kit.Button("Buscar", "BlueButton",
            () => _ = Ui.Run(RefreshAsync, "No se pudieron buscar los tickets"));
        searchButton.Margin = new Thickness(0, 0, 8, 8);
        filters.Children.Add(searchButton);
        var clearButton = Kit.Button("Limpiar", "GhostButton", Clear);
        clearButton.Margin = new Thickness(0, 0, 8, 8);
        filters.Children.Add(clearButton);

        var footer = Kit.HStack(8, _count,
            Kit.Muted("Para tickets más antiguos, amplíe el rango de fechas. Se muestran hasta 500 resultados."));
        footer.Margin = new Thickness(0, 0, 0, 8);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(Kit.PageHeader("Buscador de tickets",
            "Localice comprobantes antiguos por número, cliente/RIF o producto; consulte el detalle y reimprímalos."));
        Grid.SetRow(filters, 1);
        root.Children.Add(filters);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        Grid.SetRow(_grid, 3);
        root.Children.Add(_grid);

        Content = root;
    }

    private static FrameworkElement Filter(string label, FrameworkElement input)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 14, 8),
            VerticalAlignment = VerticalAlignment.Center
        };
        var caption = Kit.Label(label);
        caption.Margin = new Thickness(0, 0, 5, 0);
        caption.VerticalAlignment = VerticalAlignment.Center;
        panel.Children.Add(caption);
        input.VerticalAlignment = VerticalAlignment.Center;
        panel.Children.Add(input);
        return panel;
    }

    private void Clear()
    {
        _number.Text = string.Empty;
        _customer.Text = string.Empty;
        _product.Text = string.Empty;
        _from.SelectedDate = DefaultFrom;
        _to.SelectedDate = DateTime.Today;
        _status.SelectedIndex = 0;
        _ = Ui.Run(RefreshAsync, "No se pudieron buscar los tickets");
    }

    public async Task RefreshAsync()
    {
        var from = _from.SelectedDate?.Date ?? new DateTime(2000, 1, 1);
        var to = (_to.SelectedDate?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);
        var status = (_status.SelectedItem as Option<SaleStatus?>)?.Value;

        _sales = await App.Api.SearchSalesAsync(from, to, _number.Text, _customer.Text,
            _product.Text, status, 500);
        _grid.ItemsSource = _sales;
        _count.Text = $"{_sales.Count} ticket(s) encontrado(s).";
    }

    private void OnView(object sender, RoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not SaleDto sale) return;

        _ = Ui.Run(async () =>
        {
            _settings ??= await TicketSettings.LoadAsync();
            new SaleReceiptDialog(sale, _settings).ShowDialog();
        }, "No se pudo abrir el detalle del ticket");
    }

    private void OnPrint(object sender, RoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not SaleDto sale) return;

        _ = Ui.Run(async () =>
        {
            _settings ??= await TicketSettings.LoadAsync();
            if (!TicketPrinter.PrintSale(sale, _settings))
                Ui.Info("No se seleccionó una impresora.", "Impresión cancelada");
        }, "No se pudo imprimir el ticket");
    }
}
