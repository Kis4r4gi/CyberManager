using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

public sealed class ProductsView : UserControl, IRefreshable
{
    private readonly DataGrid _productsGrid;
    private readonly DataGrid _categoriesGrid;
    private readonly DataGrid _ratesGrid;
    private readonly DataGrid _packagesGrid;
    private readonly DataGrid _zonesGrid;

    private List<ProductDto> _products = new();
    private List<CategoryDto> _categories = new();
    private List<RateDto> _rates = new();
    private List<TimePackageDto> _packages = new();
    private List<ZoneDto> _zones = new();

    public ProductsView()
    {
        _productsGrid = Kit.Table(
            Kit.Col("Código", nameof(ProductRow.Sku), 110),
            Kit.Col("Producto", nameof(ProductRow.Name)),
            Kit.Col("Categoría", nameof(ProductRow.Category), 140),
            Kit.Col("Precio USD", nameof(ProductRow.Price), 100, "{0:N2}"),
            Kit.Col("Costo", nameof(ProductRow.Cost), 90, "{0:N2}"),
            Kit.Col("Stock", nameof(ProductRow.Stock), 80),
            Kit.Col("Mínimo", nameof(ProductRow.MinStock), 80),
            Kit.Col("IVA", nameof(ProductRow.Tax), 70),
            Kit.Col("Estado", nameof(ProductRow.Status), 100));

        _categoriesGrid = Kit.Table(
            Kit.Col("Categoría", nameof(CategoryRow.Name)),
            Kit.Col("Color", nameof(CategoryRow.Color), 110),
            Kit.Col("Orden", nameof(CategoryRow.SortOrder), 80),
            Kit.Col("Estado", nameof(CategoryRow.Status), 110));

        _ratesGrid = Kit.Table(
            Kit.Col("Tarifa", nameof(RateRow.Name)),
            Kit.Col("Precio por hora", nameof(RateRow.Price), 130, "{0:N2}"),
            Kit.Col("Mínimo (min)", nameof(RateRow.Minimum), 110),
            Kit.Col("Bloque (min)", nameof(RateRow.Block), 110),
            Kit.Col("Estado", nameof(RateRow.Status), 110));

        _packagesGrid = Kit.Table(
            Kit.Col("Paquete", nameof(PackageRow.Name)),
            Kit.Col("Minutos", nameof(PackageRow.Minutes), 100),
            Kit.Col("Bono", nameof(PackageRow.Bonus), 90),
            Kit.Col("Precio USD", nameof(PackageRow.Price), 110, "{0:N2}"),
            Kit.Col("Estado", nameof(PackageRow.Status), 110));

        _zonesGrid = Kit.Table(
            Kit.Col("Zona", nameof(ZoneRow.Name)),
            Kit.Col("Color", nameof(ZoneRow.Color), 110),
            Kit.Col("Orden", nameof(ZoneRow.SortOrder), 80),
            Kit.Col("Estado", nameof(ZoneRow.Status), 110));

        var tabs = new TabControl { Margin = new Thickness(0, 4, 0, 0) };
        tabs.Items.Add(new TabItem { Header = "Productos", Content = BuildProductTab() });
        tabs.Items.Add(new TabItem { Header = "Categorías", Content = BuildCategoryTab() });
        tabs.Items.Add(new TabItem { Header = "Tarifas de tiempo", Content = BuildRateTab() });
        tabs.Items.Add(new TabItem { Header = "Paquetes de tiempo", Content = BuildPackageTab() });
        tabs.Items.Add(new TabItem { Header = "Zonas", Content = BuildZoneTab() });

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(Kit.PageHeader("Productos y tarifas",
            "Catálogo de productos, precios con IVA incluido, tarifas por hora, paquetes y zonas del local.",
            Kit.Button("Actualizar", "GhostButton", () => _ = RefreshAsync())));
        Grid.SetRow(tabs, 1);
        root.Children.Add(tabs);

        Content = root;

        _productsGrid.MouseDoubleClick += (_, _) => EditProduct();
        _categoriesGrid.MouseDoubleClick += (_, _) => EditCategory();
        _ratesGrid.MouseDoubleClick += (_, _) => EditRate();
        _packagesGrid.MouseDoubleClick += (_, _) => EditPackage();
        _zonesGrid.MouseDoubleClick += (_, _) => EditZone();

        Loaded += async (_, _) => await Ui.Run(RefreshAsync, "No se pudo cargar el catálogo");
    }

    public async Task RefreshAsync()
    {
        var bootstrap = await App.Api.BootstrapAsync();
        _products = await App.Api.ProductsAsync(includeInactive: true);
        _categories = await App.Api.CategoriesAsync();
        _rates = await App.Api.RatesAsync();
        _packages = await App.Api.PackagesAsync();
        _zones = await App.Api.ZonesAsync();

        _productsGrid.ItemsSource = _products
            .OrderBy(p => p.Name)
            .Select(p => new ProductRow(p))
            .ToList();

        _categoriesGrid.ItemsSource = _categories
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryRow(c))
            .ToList();

        _ratesGrid.ItemsSource = _rates
            .OrderBy(r => r.PricePerHour)
            .Select(r => new RateRow(r))
            .ToList();

        _packagesGrid.ItemsSource = _packages
            .OrderBy(p => p.Minutes)
            .Select(p => new PackageRow(p))
            .ToList();

        _zonesGrid.ItemsSource = _zones
            .OrderBy(z => z.SortOrder).ThenBy(z => z.Name)
            .Select(z => new ZoneRow(z))
            .ToList();
    }

    // ================= Productos =================
    private Grid BuildProductTab()
    {
        var actions = Kit.HStack(8,
            Kit.Button("Nuevo producto", "PrimaryButton", NewProduct),
            Kit.Button("Editar", "GhostButton", EditProduct),
            Kit.Button("Activar / desactivar", "GhostButton", ToggleProduct));

        return ListTab("Los precios se capturan con IVA incluido; el sistema desglosa el impuesto al facturar.",
            actions, _productsGrid);
    }

    private static Grid ListTab(string description, FrameworkElement actions, DataGrid grid)
    {
        var root = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(new TextBlock
        {
            Text = description,
            Style = Kit.Style("Muted"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        actions.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetRow(actions, 1);
        root.Children.Add(actions);

        Grid.SetRow(grid, 2);
        grid.Margin = new Thickness(0, 12, 0, 0);
        root.Children.Add(grid);

        return root;
    }

    private void NewProduct()
    {
        var dialog = new ProductDialog(_categories) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveProductAsync(null, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar el producto");
    }

    private void EditProduct()
    {
        if (_productsGrid.SelectedItem is not ProductRow row) return;
        var dialog = new ProductDialog(_categories, row.Source) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveProductAsync(row.Source.Id, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar el producto");
    }

    private void ToggleProduct()
    {
        if (_productsGrid.SelectedItem is not ProductRow row) return;
        var product = row.Source;
        if (!Ui.Confirm($"¿{(product.Active ? "Desactivar" : "Activar")} {product.Name}?", "Producto")) return;

        _ = Ui.Run(async () =>
        {
            var request = new SaveProductRequest(product.Sku, product.Name, product.CategoryId,
                product.Price, product.Cost, product.Stock, product.MinStock, product.Unit,
                product.IsService, product.TaxRate, !product.Active);
            await App.Api.SaveProductAsync(product.Id, request);
            await RefreshAsync();
        }, "No se pudo actualizar el producto");
    }

    // ================= Categorías =================
    private Grid BuildCategoryTab()
    {
        var actions = Kit.HStack(8,
            Kit.Button("Nueva categoría", "PrimaryButton", NewCategory),
            Kit.Button("Editar", "GhostButton", EditCategory));

        return ListTab("Agrupe productos para encontrarlos más rápido en la caja y en la pantalla del cliente.",
            actions, _categoriesGrid);
    }

    private void NewCategory()
    {
        var dialog = new CategoryDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveCategoryAsync(null, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar la categoría");
    }

    private void EditCategory()
    {
        if (_categoriesGrid.SelectedItem is not CategoryRow row) return;
        var dialog = new CategoryDialog(row.Source) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveCategoryAsync(row.Source.Id, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar la categoría");
    }

    // ================= Tarifas =================
    private Grid BuildRateTab()
    {
        var actions = Kit.HStack(8,
            Kit.Button("Nueva tarifa", "PrimaryButton", NewRate),
            Kit.Button("Editar", "GhostButton", EditRate));

        return ListTab("La tarifa se aplica por minuto; el mínimo y el bloque definen cómo se redondea el cobro.",
            actions, _ratesGrid);
    }

    private void NewRate()
    {
        var dialog = new RateDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveRateAsync(null, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar la tarifa");
    }

    private void EditRate()
    {
        if (_ratesGrid.SelectedItem is not RateRow row) return;
        var dialog = new RateDialog(row.Source) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveRateAsync(row.Source.Id, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar la tarifa");
    }

    // ================= Paquetes =================
    private Grid BuildPackageTab()
    {
        var actions = Kit.HStack(8,
            Kit.Button("Nuevo paquete", "PrimaryButton", NewPackage),
            Kit.Button("Editar", "GhostButton", EditPackage));

        return ListTab("Paquetes prepagados con minutos de bono, por ejemplo 60 + 15 minutos.",
            actions, _packagesGrid);
    }

    private void NewPackage()
    {
        var dialog = new PackageDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SavePackageAsync(null, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar el paquete");
    }

    private void EditPackage()
    {
        if (_packagesGrid.SelectedItem is not PackageRow row) return;
        var dialog = new PackageDialog(row.Source) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SavePackageAsync(row.Source.Id, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar el paquete");
    }

    // ================= Zonas =================
    private Grid BuildZoneTab()
    {
        var actions = Kit.HStack(8,
            Kit.Button("Nueva zona", "PrimaryButton", NewZone),
            Kit.Button("Editar", "GhostButton", EditZone));

        return ListTab("Las zonas agrupan equipos (VIP, estándar, consolas) y pueden tener tarifas distintas.",
            actions, _zonesGrid);
    }

    private void NewZone()
    {
        var dialog = new ZoneDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveZoneAsync(null, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar la zona");
    }

    private void EditZone()
    {
        if (_zonesGrid.SelectedItem is not ZoneRow row) return;
        var dialog = new ZoneDialog(row.Source) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveZoneAsync(row.Source.Id, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar la zona");
    }
}

// ================= Filas =================
public sealed class ProductRow
{
    public ProductRow(ProductDto product)
    {
        Source = product;
        Sku = product.Sku;
        Name = product.Name;
        Category = product.CategoryName ?? "General";
        Stock = product.IsService ? "Servicio" : $"{product.Stock:N0} {product.Unit}";
        MinStock = product.IsService ? "—" : product.MinStock.ToString("N0");
        Tax = $"{product.TaxRate:N0}%";
        Status = product.Active ? (product.LowStock ? "Stock bajo" : "Activo") : "Inactivo";
        StatusBrush = !product.Active ? "#8FA4C4" : product.LowStock ? "#F59E0B" : "#22C55E";
        Price = product.Price;
        Cost = product.Cost;
    }

    public ProductDto Source { get; }
    public string Sku { get; }
    public string Name { get; }
    public string Category { get; }
    public decimal Price { get; }
    public decimal Cost { get; }
    public string Stock { get; }
    public string MinStock { get; }
    public string Tax { get; }
    public string Status { get; }
    public string StatusBrush { get; }
}

public sealed class CategoryRow
{
    public CategoryRow(CategoryDto category)
    {
        Source = category;
        Name = category.Name;
        Color = category.Color;
        SortOrder = category.SortOrder;
        Status = category.Active ? "Activa" : "Inactiva";
    }

    public CategoryDto Source { get; }
    public string Name { get; }
    public string Color { get; }
    public int SortOrder { get; }
    public string Status { get; }
}

public sealed class RateRow
{
    public RateRow(RateDto rate)
    {
        Source = rate;
        Name = rate.Name;
        Price = rate.PricePerHour;
        Minimum = rate.MinimumMinutes;
        Block = rate.BillingBlockMinutes;
        Status = rate.Active ? "Activa" : "Inactiva";
    }

    public RateDto Source { get; }
    public string Name { get; }
    public decimal Price { get; }
    public int Minimum { get; }
    public int Block { get; }
    public string Status { get; }
}

public sealed class PackageRow
{
    public PackageRow(TimePackageDto package)
    {
        Source = package;
        Name = package.Name;
        Minutes = package.Minutes;
        Bonus = package.BonusMinutes;
        Price = package.Price;
        Status = package.Active ? "Activo" : "Inactivo";
    }

    public TimePackageDto Source { get; }
    public string Name { get; }
    public int Minutes { get; }
    public int Bonus { get; }
    public decimal Price { get; }
    public string Status { get; }
}

public sealed class ZoneRow
{
    public ZoneRow(ZoneDto zone)
    {
        Source = zone;
        Name = zone.Name;
        Color = zone.Color;
        SortOrder = zone.SortOrder;
        Status = zone.Active ? "Activa" : "Inactiva";
    }

    public ZoneDto Source { get; }
    public string Name { get; }
    public string Color { get; }
    public int SortOrder { get; }
    public string Status { get; }
}

// ================= Diálogos =================
public sealed class ProductDialog : FormDialog
{
    private readonly TextBox _sku;
    private readonly TextBox _name;
    private readonly ComboBox _category;
    private readonly TextBox _price;
    private readonly TextBox _cost;
    private readonly TextBox _stock;
    private readonly TextBox _minStock;
    private readonly TextBox _unit;
    private readonly TextBox _tax;
    private readonly CheckBox _service;
    private readonly CheckBox _active;

    public SaveProductRequest? Result { get; private set; }

    public ProductDialog(List<CategoryDto> categories, ProductDto? product = null)
        : base(product is null ? "Nuevo producto" : $"Editar {product.Name}",
            "El precio se escribe con IVA incluido. El stock solo aplica a productos físicos.")
    {
        _sku = Add(new TextBox { Text = product?.Sku ?? string.Empty }, "Código / SKU");
        _name = Add(new TextBox { Text = product?.Name ?? string.Empty }, "Nombre del producto");

        var categoryOptions = new List<Option<int?>> { new(null, "Sin categoría") };
        categoryOptions.AddRange(categories.Where(c => c.Active).OrderBy(c => c.Name)
            .Select(c => new Option<int?>(c.Id, c.Name)));
        _category = AddCombo("Categoría", categoryOptions);
        if (product?.CategoryId is int categoryId)
        {
            var option = categoryOptions.FirstOrDefault(o => o.Value == categoryId);
            if (option is not null) _category.SelectedItem = option;
        }

        _price = Add(new TextBox { Text = Money(product?.Price) }, "Precio de venta (USD, IVA incluido)");
        _cost = Add(new TextBox { Text = Money(product?.Cost) }, "Costo (USD)");
        _stock = Add(new TextBox { Text = (product?.Stock ?? 0).ToString("0.##") }, "Stock actual");
        _minStock = Add(new TextBox { Text = (product?.MinStock ?? 0).ToString("0.##") }, "Stock mínimo para alerta");
        _unit = Add(new TextBox { Text = product?.Unit ?? "und" }, "Unidad de medida");
        _tax = Add(new TextBox { Text = (product?.TaxRate ?? 16m).ToString("0.##") }, "IVA (%)");

        _service = AddCheck("Es un servicio (no controla inventario)", product?.IsService ?? false);
        _active = AddCheck("Producto activo", product?.Active ?? true);
    }

    private static string Money(decimal? value) => (value ?? 0).ToString("N2", CultureInfo.CurrentCulture);

    protected override void OnAccept()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
            throw new InvalidOperationException("Indique el nombre del producto.");

        var sku = string.IsNullOrWhiteSpace(_sku.Text)
            ? $"SKU-{DateTime.Now:HHmmss}"
            : _sku.Text.Trim();

        var categoryId = (_category.SelectedItem as Option<int?>)?.Value;
        var price = ParseDecimal(_price, "Precio", 0.01m);
        var cost = ParseDecimal(_cost, "Costo");
        var stock = ParseDecimal(_stock, "Stock");
        var minStock = ParseDecimal(_minStock, "Stock mínimo");
        var tax = ParseDecimal(_tax, "IVA");

        Result = new SaveProductRequest(sku, _name.Text.Trim(), categoryId, price, cost, stock, minStock,
            string.IsNullOrWhiteSpace(_unit.Text) ? "und" : _unit.Text.Trim(),
            _service.IsChecked == true, tax, _active.IsChecked == true);

        base.OnAccept();
    }
}

public sealed class CategoryDialog : FormDialog
{
    private readonly TextBox _name;
    private readonly TextBox _color;
    private readonly TextBox _sort;
    private readonly CheckBox _active;

    public SaveCategoryRequest? Result { get; private set; }

    public CategoryDialog(CategoryDto? category = null)
        : base(category is null ? "Nueva categoría" : "Editar categoría")
    {
        _name = Add(new TextBox { Text = category?.Name ?? string.Empty }, "Nombre");
        _color = Add(new TextBox { Text = category?.Color ?? "#22D3EE" }, "Color (hexadecimal)");
        _sort = Add(new TextBox { Text = (category?.SortOrder ?? 0).ToString() }, "Orden");
        _active = AddCheck("Categoría activa", category?.Active ?? true);
    }

    protected override void OnAccept()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
            throw new InvalidOperationException("Indique el nombre de la categoría.");

        Result = new SaveCategoryRequest(_name.Text.Trim(),
            string.IsNullOrWhiteSpace(_color.Text) ? "#22D3EE" : _color.Text.Trim(),
            ParseInt(_sort, "Orden"), _active.IsChecked == true);
        base.OnAccept();
    }
}

public sealed class RateDialog : FormDialog
{
    private readonly TextBox _name;
    private readonly TextBox _price;
    private readonly TextBox _minimum;
    private readonly TextBox _block;
    private readonly CheckBox _active;

    public SaveRateRequest? Result { get; private set; }

    public RateDialog(RateDto? rate = null)
        : base(rate is null ? "Nueva tarifa" : "Editar tarifa", "El cobro se calcula por minuto y se redondea al bloque.")
    {
        _name = Add(new TextBox { Text = rate?.Name ?? string.Empty }, "Nombre");
        _price = Add(new TextBox { Text = (rate?.PricePerHour ?? 1m).ToString("N2", CultureInfo.CurrentCulture) }, "Precio por hora (USD)");
        _minimum = Add(new TextBox { Text = (rate?.MinimumMinutes ?? 60).ToString() }, "Minutos mínimos");
        _block = Add(new TextBox { Text = (rate?.BillingBlockMinutes ?? 1).ToString() }, "Bloque de cobro (minutos)");
        _active = AddCheck("Tarifa activa", rate?.Active ?? true);
    }

    protected override void OnAccept()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
            throw new InvalidOperationException("Indique el nombre de la tarifa.");

        Result = new SaveRateRequest(_name.Text.Trim(),
            ParseDecimal(_price, "Precio por hora", 0.01m),
            ParseInt(_minimum, "Minutos mínimos", 1),
            ParseInt(_block, "Bloque de cobro", 1),
            _active.IsChecked == true);
        base.OnAccept();
    }
}

public sealed class PackageDialog : FormDialog
{
    private readonly TextBox _name;
    private readonly TextBox _minutes;
    private readonly TextBox _bonus;
    private readonly TextBox _price;
    private readonly CheckBox _active;

    public SaveTimePackageRequest? Result { get; private set; }

    public PackageDialog(TimePackageDto? package = null)
        : base(package is null ? "Nuevo paquete" : "Editar paquete")
    {
        _name = Add(new TextBox { Text = package?.Name ?? string.Empty }, "Nombre");
        _minutes = Add(new TextBox { Text = (package?.Minutes ?? 60).ToString() }, "Minutos incluidos");
        _bonus = Add(new TextBox { Text = (package?.BonusMinutes ?? 0).ToString() }, "Minutos de bono");
        _price = Add(new TextBox { Text = (package?.Price ?? 1m).ToString("N2", CultureInfo.CurrentCulture) }, "Precio (USD)");
        _active = AddCheck("Paquete activo", package?.Active ?? true);
    }

    protected override void OnAccept()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
            throw new InvalidOperationException("Indique el nombre del paquete.");

        Result = new SaveTimePackageRequest(_name.Text.Trim(),
            ParseInt(_minutes, "Minutos incluidos", 1),
            ParseInt(_bonus, "Minutos de bono"),
            ParseDecimal(_price, "Precio", 0.01m),
            _active.IsChecked == true);
        base.OnAccept();
    }
}

public sealed class ZoneDialog : FormDialog
{
    private readonly TextBox _name;
    private readonly TextBox _color;
    private readonly TextBox _sort;
    private readonly CheckBox _active;

    public SaveZoneRequest? Result { get; private set; }

    public ZoneDialog(ZoneDto? zone = null)
        : base(zone is null ? "Nueva zona" : "Editar zona")
    {
        _name = Add(new TextBox { Text = zone?.Name ?? string.Empty }, "Nombre");
        _color = Add(new TextBox { Text = zone?.Color ?? "#3B82F6" }, "Color (hexadecimal)");
        _sort = Add(new TextBox { Text = (zone?.SortOrder ?? 0).ToString() }, "Orden");
        _active = AddCheck("Zona activa", zone?.Active ?? true);
    }

    protected override void OnAccept()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
            throw new InvalidOperationException("Indique el nombre de la zona.");

        Result = new SaveZoneRequest(_name.Text.Trim(),
            string.IsNullOrWhiteSpace(_color.Text) ? "#3B82F6" : _color.Text.Trim(),
            ParseInt(_sort, "Orden"), _active.IsChecked == true);
        base.OnAccept();
    }
}
