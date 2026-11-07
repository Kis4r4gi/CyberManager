using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

public sealed class InventoryView : UserControl, IRefreshable
{
    private readonly DataGrid _productsGrid;
    private readonly DataGrid _movementsGrid;
    private readonly ComboBox _productBox = new();
    private readonly ComboBox _typeBox = new();
    private readonly TextBox _quantityBox = new();
    private readonly TextBox _costBox = new();
    private readonly TextBox _referenceBox = new();
    private readonly TextBox _notesBox = new();
    private readonly TextBlock _kardexTitle = Kit.Muted("Kardex general");
    private readonly TextBlock _summaryText = Kit.Muted("Cargando inventario...");

    private List<ProductDto> _products = new();
    private int? _kardexProductId;

    public InventoryView()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(Kit.PageHeader("Inventario y kardex",
            "Existencias, ajustes, mermas y trazabilidad de cada producto.",
            Kit.Button("Actualizar", "GhostButton", () => _ = RefreshAsync())));

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(382) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(content, 1);
        root.Children.Add(content);

        // ================= Formulario =================
        var form = new StackPanel();
        form.Children.Add(Kit.Section("Registrar movimiento"));
        form.Children.Add(new TextBlock
        {
            Text = "Use ajustes para corregir inventario, mermas para productos dañados y devoluciones para reingresos.",
            Style = Kit.Style("Muted"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        _productBox.SelectionChanged += (_, _) =>
        {
            if (_productBox.SelectedItem is Option<ProductDto> option && _costBox.Text is "0,00" or "")
                _costBox.Text = option.Value.Cost.ToString("N2", CultureInfo.CurrentCulture);
        };
        form.Children.Add(Kit.Field("Producto", _productBox));

        _typeBox.ItemsSource = new List<Option<StockMovementType>>
        {
            new(StockMovementType.Adjustment, "Ajuste de inventario"),
            new(StockMovementType.Waste, "Merma (descuenta)"),
            new(StockMovementType.Return, "Devolución (suma)"),
            new(StockMovementType.Initial, "Carga inicial (suma)")
        };
        _typeBox.DisplayMemberPath = nameof(Option<object>.DisplayText);
        _typeBox.SelectedIndex = 0;
        form.Children.Add(Kit.Field("Tipo de movimiento", _typeBox));

        _quantityBox.Text = "1";
        form.Children.Add(Kit.Field("Cantidad", _quantityBox));

        _costBox.Text = "0,00";
        form.Children.Add(Kit.Field("Costo unitario (USD)", _costBox));

        form.Children.Add(Kit.Field("Referencia", _referenceBox));
        form.Children.Add(Kit.Field("Notas", _notesBox));

        var submit = Kit.Button("Registrar movimiento", "PrimaryButton", SubmitMovement);
        submit.Padding = new Thickness(14, 11, 14, 11);
        submit.Margin = new Thickness(0, 6, 0, 0);
        form.Children.Add(submit);

        form.Children.Add(new Separator { Margin = new Thickness(0, 18, 0, 12) });
        form.Children.Add(_summaryText);

        var formCard = Kit.Card(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = form
        }, 16);
        formCard.Margin = new Thickness(0, 0, 14, 0);
        content.Children.Add(formCard);

        // ================= Tablas =================
        _productsGrid = Kit.Table(
            Kit.Col("SKU", nameof(StockRow.Sku), 100),
            Kit.Col("Producto", nameof(StockRow.Name)),
            Kit.Col("Categoría", nameof(StockRow.Category), 120),
            Kit.Col("Stock", nameof(StockRow.Stock), 90),
            Kit.Col("Mínimo", nameof(StockRow.MinStock), 80),
            Kit.Col("Costo", nameof(StockRow.Cost), 90, "{0:N2}"),
            Kit.Col("Precio", nameof(StockRow.Price), 90, "{0:N2}"),
            Kit.Col("Estado", nameof(StockRow.Status), 105));

        _movementsGrid = Kit.Table(
            Kit.Col("Fecha", nameof(KardexRow.Date), 130),
            Kit.Col("Producto", nameof(KardexRow.Product)),
            Kit.Col("Tipo", nameof(KardexRow.Type), 100),
            Kit.Col("Cantidad", nameof(KardexRow.Quantity), 90, "{0:N2}"),
            Kit.Col("Costo", nameof(KardexRow.Cost), 80, "{0:N2}"),
            Kit.Col("Saldo", nameof(KardexRow.Balance), 80, "{0:N2}"),
            Kit.Col("Referencia", nameof(KardexRow.Reference), 110),
            Kit.Col("Usuario", nameof(KardexRow.User), 120));

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });

        var stockHeader = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        stockHeader.Children.Add(Kit.Section("Existencias"));
        var kardexButton = Kit.SmallButton("Ver kardex del producto seleccionado", "GhostButton", FilterKardex);
        kardexButton.HorizontalAlignment = HorizontalAlignment.Right;
        stockHeader.Children.Add(kardexButton);

        var stockGrid = new Grid();
        stockGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        stockGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        stockGrid.Children.Add(stockHeader);
        Grid.SetRow(_productsGrid, 1);
        stockGrid.Children.Add(_productsGrid);

        var stockCard = Kit.Card(stockGrid, 14);
        stockCard.Margin = new Thickness(0, 0, 0, 12);
        right.Children.Add(stockCard);

        var kardexHeader = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        kardexHeader.Children.Add(_kardexTitle);
        var allButton = Kit.SmallButton("Ver todo el kardex", "GhostButton", ShowAllKardex);
        allButton.HorizontalAlignment = HorizontalAlignment.Right;
        kardexHeader.Children.Add(allButton);

        var kardexGrid = new Grid();
        kardexGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        kardexGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        kardexGrid.Children.Add(kardexHeader);
        Grid.SetRow(_movementsGrid, 1);
        kardexGrid.Children.Add(_movementsGrid);

        var kardexCard = Kit.Card(kardexGrid, 14);
        Grid.SetRow(kardexCard, 1);
        right.Children.Add(kardexCard);

        Grid.SetColumn(right, 1);
        content.Children.Add(right);

        Content = root;

        _productsGrid.MouseDoubleClick += (_, _) =>
        {
            if (_productsGrid.SelectedItem is StockRow row)
            {
                var option = ((List<Option<ProductDto>>)_productBox.ItemsSource)
                    .FirstOrDefault(o => o.Value.Id == row.Source.Id);
                if (option is not null) _productBox.SelectedItem = option;
            }
        };

        Loaded += async (_, _) => await Ui.Run(RefreshAsync, "No se pudo cargar el inventario");
    }

    public async Task RefreshAsync()
    {
        _products = await App.Api.ProductsAsync();

        var previous = (_productBox.SelectedItem as Option<ProductDto>)?.Value.Id;
        var options = _products.Select(p => new Option<ProductDto>(p,
            $"{p.Name} · stock {p.Stock:N0} {p.Unit}")).ToList();
        _productBox.ItemsSource = options;
        _productBox.DisplayMemberPath = nameof(Option<object>.DisplayText);
        _productBox.SelectedIndex = previous is int id
            ? Math.Max(0, options.FindIndex(o => o.Value.Id == id))
            : options.Count > 0 ? 0 : -1;

        _productsGrid.ItemsSource = _products
            .OrderBy(p => p.Name)
            .Select(p => new StockRow(p))
            .ToList();

        var lowStock = _products.Count(p => !p.IsService && p.LowStock);
        var costValue = _products.Where(p => !p.IsService).Sum(p => p.Stock * p.Cost);
        var retailValue = _products.Where(p => !p.IsService).Sum(p => p.Stock * p.Price);

        _summaryText.Text =
            $"{_products.Count} productos activos · {lowStock} bajo el mínimo\n" +
            $"Valor al costo: $ {costValue:N2}\n" +
            $"Valor de venta: $ {retailValue:N2}";

        await LoadKardexAsync();
    }

    private async Task LoadKardexAsync()
    {
        var movements = await App.Api.StockMovementsAsync(_kardexProductId);
        _movementsGrid.ItemsSource = movements.Select(m => new KardexRow(m)).ToList();

        var product = _products.FirstOrDefault(p => p.Id == _kardexProductId);
        _kardexTitle.Text = product is null ? "Kardex general" : $"Kardex · {product.Name}";
    }

    private void FilterKardex()
    {
        if (_productBox.SelectedItem is not Option<ProductDto> option)
        {
            Ui.Info("Seleccione un producto para ver su kardex.", "Kardex");
            return;
        }

        _kardexProductId = option.Value.Id;
        _ = Ui.Run(LoadKardexAsync, "No se pudo cargar el kardex");
    }

    private void ShowAllKardex()
    {
        _kardexProductId = null;
        _ = Ui.Run(LoadKardexAsync, "No se pudo cargar el kardex");
    }

    private void SubmitMovement()
    {
        if (_productBox.SelectedItem is not Option<ProductDto> option)
        {
            Ui.Error("Seleccione un producto.", "Inventario");
            return;
        }

        var type = (_typeBox.SelectedItem as Option<StockMovementType>)?.Value ?? StockMovementType.Adjustment;

        if (!decimal.TryParse(_quantityBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var quantity)
            || quantity <= 0)
        {
            Ui.Error("La cantidad debe ser un número mayor que cero.", "Inventario");
            return;
        }

        var cost = decimal.TryParse(_costBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var c) ? c : 0m;
        var reference = string.IsNullOrWhiteSpace(_referenceBox.Text) ? null : _referenceBox.Text.Trim();
        var notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim();

        var request = new StockMovementRequest(option.Value.Id, type, quantity, cost, reference, notes);

        _ = Ui.Run(async () =>
        {
            await App.Api.AddStockMovementAsync(request);
            _quantityBox.Text = "1";
            _referenceBox.Text = string.Empty;
            _notesBox.Text = string.Empty;
            await RefreshAsync();
        }, "No se pudo registrar el movimiento");
    }
}

public sealed class StockRow
{
    public StockRow(ProductDto product)
    {
        Source = product;
        Sku = product.Sku;
        Name = product.Name;
        Category = product.CategoryName ?? "General";
        Stock = product.IsService ? "Servicio" : $"{product.Stock:N0} {product.Unit}";
        MinStock = product.IsService ? "—" : product.MinStock.ToString("N0");
        Cost = product.Cost;
        Price = product.Price;
        Status = product.IsService ? "Servicio" : product.LowStock ? "Stock bajo" : "Disponible";
        StatusBrush = product.IsService ? "#8B5CF6" : product.LowStock ? "#F59E0B" : "#22C55E";
    }

    public ProductDto Source { get; }
    public string Sku { get; }
    public string Name { get; }
    public string Category { get; }
    public string Stock { get; }
    public string MinStock { get; }
    public decimal Cost { get; }
    public decimal Price { get; }
    public string Status { get; }
    public string StatusBrush { get; }
}

public sealed class KardexRow
{
    public KardexRow(StockMovementDto movement)
    {
        Date = movement.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        Product = movement.ProductName;
        Type = Labels.StockName(movement.Type);
        Quantity = movement.Quantity;
        Cost = movement.UnitCost;
        Balance = movement.BalanceAfter;
        Reference = movement.Reference ?? "—";
        User = movement.UserName ?? "—";
    }

    public string Date { get; }
    public string Product { get; }
    public string Type { get; }
    public decimal Quantity { get; }
    public decimal Cost { get; }
    public decimal Balance { get; }
    public string Reference { get; }
    public string User { get; }
}

