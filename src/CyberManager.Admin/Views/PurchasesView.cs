using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

public sealed class PurchasesView : UserControl, IRefreshable
{
    private readonly DataGrid _suppliersGrid;
    private readonly DataGrid _purchasesGrid;
    private List<SupplierDto> _suppliers = new();
    private List<ProductDto> _products = new();
    private List<PurchaseDto> _purchases = new();
    private Dictionary<int, SupplierBalanceDto> _balances = new();

    public PurchasesView()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(Kit.PageHeader("Compras y proveedores",
            "Registre compras para aumentar el inventario y actualizar el costo de los productos.",
            Kit.Button("Actualizar", "GhostButton", () => _ = RefreshAsync())));

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(390) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(content, 1);
        root.Children.Add(content);

        // ================= Proveedores =================
        _suppliersGrid = Kit.Table(
            Kit.Col("Proveedor", nameof(SupplierRow.Name)),
            Kit.Col("Teléfono", nameof(SupplierRow.Phone), 100),
            Kit.Col("Saldo USD", nameof(SupplierRow.Balance), 90, "{0:N2}"),
            Kit.Col("Estado", nameof(SupplierRow.Status), 80));

        var supplierGrid = new Grid();
        supplierGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        supplierGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        supplierGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        supplierGrid.Children.Add(Kit.Section("Proveedores"));

        var supplierActions = Kit.HStack(8,
            Kit.Button("Nuevo", "PrimaryButton", NewSupplier),
            Kit.Button("Editar", "GhostButton", EditSupplier),
            Kit.Button("Abonar", "SuccessButton", RegisterPayment),
            Kit.Button("Historial", "BlueButton", ShowPayments));
        supplierActions.Margin = new Thickness(0, 0, 0, 10);
        Grid.SetRow(supplierActions, 1);
        supplierGrid.Children.Add(supplierActions);

        Grid.SetRow(_suppliersGrid, 2);
        supplierGrid.Children.Add(_suppliersGrid);

        var supplierCard = Kit.Card(supplierGrid, 14);
        supplierCard.Margin = new Thickness(0, 0, 14, 0);
        content.Children.Add(supplierCard);

        // ================= Compras =================
        _purchasesGrid = Kit.Table(
            Kit.Col("N.º", nameof(PurchaseRow.Id), 60),
            Kit.Col("Fecha", nameof(PurchaseRow.Date), 130),
            Kit.Col("Proveedor", nameof(PurchaseRow.Supplier)),
            Kit.Col("Factura", nameof(PurchaseRow.Invoice), 130),
            Kit.Col("Artículos", nameof(PurchaseRow.ItemCount), 90),
            Kit.Col("Total USD", nameof(PurchaseRow.Total), 110, "{0:N2}"));

        var purchaseGrid = new Grid();
        purchaseGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        purchaseGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var purchaseHeader = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        purchaseHeader.Children.Add(Kit.Section("Compras registradas"));
        var newPurchase = Kit.Button("Registrar compra", "PrimaryButton", NewPurchase);
        newPurchase.HorizontalAlignment = HorizontalAlignment.Right;
        purchaseHeader.Children.Add(newPurchase);
        purchaseGrid.Children.Add(purchaseHeader);

        Grid.SetRow(_purchasesGrid, 1);
        purchaseGrid.Children.Add(_purchasesGrid);

        var purchaseCard = Kit.Card(purchaseGrid, 14);
        Grid.SetColumn(purchaseCard, 1);
        content.Children.Add(purchaseCard);

        Content = root;

        _suppliersGrid.MouseDoubleClick += (_, _) => EditSupplier();
        _purchasesGrid.MouseDoubleClick += (_, _) => ShowPurchase();

        Loaded += async (_, _) => await Ui.Run(RefreshAsync, "No se pudieron cargar las compras");
    }

    public async Task RefreshAsync()
    {
        var suppliersTask = App.Api.SuppliersAsync();
        var productsTask = App.Api.ProductsAsync();
        var purchasesTask = App.Api.PurchasesAsync();
        var balancesTask = App.Api.SupplierBalancesAsync();

        await Task.WhenAll(suppliersTask, productsTask, purchasesTask, balancesTask);

        _suppliers = await suppliersTask;
        _products = await productsTask;
        _purchases = await purchasesTask;
        _balances = (await balancesTask).ToDictionary(b => b.SupplierId);

        _suppliersGrid.ItemsSource = _suppliers
            .OrderBy(s => s.Name)
            .Select(s => new SupplierRow(s, _balances.GetValueOrDefault(s.Id)))
            .ToList();

        _purchasesGrid.ItemsSource = _purchases
            .OrderByDescending(p => p.DateUtc)
            .Select(p => new PurchaseRow(p))
            .ToList();
    }

    private void NewSupplier()
    {
        var dialog = new SupplierDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveSupplierAsync(null, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar el proveedor");
    }

    private void EditSupplier()
    {
        if (_suppliersGrid.SelectedItem is not SupplierRow row) return;
        var dialog = new SupplierDialog(row.Source) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveSupplierAsync(row.Source.Id, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar el proveedor");
    }

    private void RegisterPayment()
    {
        if (_suppliersGrid.SelectedItem is not SupplierRow row) return;

        var balance = _balances.GetValueOrDefault(row.Source.Id);
        var dialog = new SupplierPaymentDialog(row.Source, balance?.Balance ?? 0)
        { Owner = Window.GetWindow(this) };

        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.CreateSupplierPaymentAsync(row.Source.Id, dialog.Result);
            await RefreshAsync();
        }, "No se pudo registrar el pago al proveedor");
    }

    private void ShowPayments()
    {
        if (_suppliersGrid.SelectedItem is not SupplierRow row) return;
        var owner = Window.GetWindow(this);

        _ = Ui.Run(async () =>
        {
            var payments = await App.Api.SupplierPaymentsAsync(row.Source.Id);
            new SupplierPaymentsDialog(row.Source, payments) { Owner = owner }.ShowDialog();
        }, "No se pudo cargar el historial de pagos");
    }

    private void NewPurchase()
    {
        var dialog = new PurchaseDialog(_suppliers, _products) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.CreatePurchaseAsync(dialog.Result);
            await RefreshAsync();
        }, "No se pudo registrar la compra");
    }

    private void ShowPurchase()
    {
        if (_purchasesGrid.SelectedItem is not PurchaseRow row) return;

        var dialog = new PurchaseDetailDialog(row.Source, _products) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
    }
}

public sealed class SupplierRow
{
    public SupplierRow(SupplierDto supplier, SupplierBalanceDto? balance = null)
    {
        Source = supplier;
        Name = supplier.Name;
        Phone = supplier.Phone ?? "—";
        Balance = balance?.Balance ?? 0m;
        Purchases = balance?.Purchases ?? 0m;
        Payments = balance?.Payments ?? 0m;
        Status = supplier.Active ? "Activo" : "Inactivo";
    }

    public SupplierDto Source { get; }
    public string Name { get; }
    public string Phone { get; }
    public decimal Balance { get; }
    public decimal Purchases { get; }
    public decimal Payments { get; }
    public string Status { get; }
}

public sealed class PurchaseRow
{
    public PurchaseRow(PurchaseDto purchase)
    {
        Source = purchase;
        Id = purchase.Id;
        Date = purchase.DateUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        Supplier = purchase.SupplierName ?? "Sin proveedor";
        Invoice = purchase.InvoiceNumber ?? "—";
        ItemCount = purchase.Items.Count;
        Total = purchase.Total;
    }

    public PurchaseDto Source { get; }
    public long Id { get; }
    public string Date { get; }
    public string Supplier { get; }
    public string Invoice { get; }
    public int ItemCount { get; }
    public decimal Total { get; }
}

public sealed class SupplierDialog : FormDialog
{
    private readonly TextBox _name;
    private readonly TextBox _phone;
    private readonly TextBox _email;
    private readonly TextBox _address;
    private readonly TextBox _notes;
    private readonly CheckBox _active;

    public SaveSupplierRequest? Result { get; private set; }

    public SupplierDialog(SupplierDto? supplier = null)
        : base(supplier is null ? "Nuevo proveedor" : $"Editar {supplier.Name}")
    {
        _name = Add(new TextBox { Text = supplier?.Name ?? string.Empty }, "Nombre o razón social");
        _phone = Add(new TextBox { Text = supplier?.Phone ?? string.Empty }, "Teléfono");
        _email = Add(new TextBox { Text = supplier?.Email ?? string.Empty }, "Correo electrónico");
        _address = Add(new TextBox { Text = supplier?.Address ?? string.Empty }, "Dirección");
        _notes = Add(new TextBox { Text = supplier?.Notes ?? string.Empty }, "Notas");
        _active = AddCheck("Proveedor activo", supplier?.Active ?? true);
    }

    protected override void OnAccept()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
            throw new InvalidOperationException("Indique el nombre del proveedor.");

        Result = new SaveSupplierRequest(_name.Text.Trim(),
            NullIfEmpty(_phone.Text), NullIfEmpty(_email.Text), NullIfEmpty(_address.Text),
            NullIfEmpty(_notes.Text), _active.IsChecked == true);
        base.OnAccept();
    }

    private static string? NullIfEmpty(string text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>Registra un abono a un proveedor (afecta la caja si es en efectivo).</summary>
public sealed class SupplierPaymentDialog : FormDialog
{
    private readonly TextBox _amountBox;
    private readonly ComboBox _methodBox;
    private readonly TextBox _referenceBox;
    private readonly TextBox _notesBox;

    public CreateSupplierPaymentRequest? Result { get; private set; }

    public SupplierPaymentDialog(SupplierDto supplier, decimal balance)
        : base($"Abonar a {supplier.Name}", "El pago reduce el saldo pendiente del proveedor.")
    {
        AddRow("Saldo pendiente", $"$ {balance:N2}", highlight: true);

        _amountBox = Add(new TextBox
        {
            Text = balance > 0
                ? balance.ToString("N2", CultureInfo.CurrentCulture)
                : "0,00"
        }, "Monto del pago (USD)");

        _methodBox = AddCombo("Forma de pago", new[]
        {
            new Option<PaymentMethod>(PaymentMethod.Cash, "Efectivo en bolívares"),
            new Option<PaymentMethod>(PaymentMethod.CashUsd, "Efectivo en dólares"),
            new Option<PaymentMethod>(PaymentMethod.Card, "Punto de venta / tarjeta"),
            new Option<PaymentMethod>(PaymentMethod.Transfer, "Transferencia"),
            new Option<PaymentMethod>(PaymentMethod.PagoMovil, "Pago Móvil")
        });

        _referenceBox = Add(new TextBox(), "Referencia (opcional)");
        _notesBox = Add(new TextBox(), "Notas (opcional)");

        AddNote("Los pagos en efectivo requieren una caja abierta y se descuentan del efectivo del turno.");
    }

    protected override void OnAccept()
    {
        var amount = ParseDecimal(_amountBox, "Monto del pago", 0.01m);
        var method = ((Option<PaymentMethod>)_methodBox.SelectedItem!).Value;

        Result = new CreateSupplierPaymentRequest(amount, method,
            string.IsNullOrWhiteSpace(_referenceBox.Text) ? null : _referenceBox.Text.Trim(),
            string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim());

        base.OnAccept();
    }
}

public sealed class SupplierPaymentsDialog : FormDialog
{
    public SupplierPaymentsDialog(SupplierDto supplier, List<SupplierPaymentDto> payments)
        : base($"Pagos a {supplier.Name}", "Historial de abonos registrados (últimos 300).", width: 640)
    {
        AcceptButton.Content = "Cerrar";
        HideCancel();

        var grid = Kit.Table(
            Kit.Col("Fecha", nameof(SupplierPaymentRow.Date), 140),
            Kit.Col("Forma de pago", nameof(SupplierPaymentRow.Method), 130),
            Kit.Col("Referencia", nameof(SupplierPaymentRow.Reference), 140),
            Kit.Col("Monto USD", nameof(SupplierPaymentRow.Amount), 100, "{0:N2}"),
            Kit.Col("Notas", nameof(SupplierPaymentRow.Notes)));
        grid.ItemsSource = payments.Select(p => new SupplierPaymentRow(p)).ToList();
        grid.Height = 260;
        Body.Children.Add(grid);

        AddSeparator();
        AddRow("Pagos registrados", payments.Count.ToString());
        AddRow("Total abonado", $"$ {payments.Sum(p => p.Amount):N2}", highlight: true);
    }
}

public sealed class SupplierPaymentRow
{
    public SupplierPaymentRow(SupplierPaymentDto payment)
    {
        Date = payment.DateUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        Method = Labels.PaymentName(payment.Method);
        Reference = string.IsNullOrWhiteSpace(payment.Reference) ? "—" : payment.Reference!;
        Amount = payment.Amount;
        Notes = string.IsNullOrWhiteSpace(payment.Notes) ? "—" : payment.Notes!;
    }

    public string Date { get; }
    public string Method { get; }
    public string Reference { get; }
    public decimal Amount { get; }
    public string Notes { get; }
}

public sealed class PurchaseDialog : FormDialog
{
    private readonly ComboBox _supplierBox;
    private readonly TextBox _invoiceBox;
    private readonly TextBox _notesBox;
    private readonly ComboBox _productBox;
    private readonly TextBox _quantityBox;
    private readonly TextBox _costBox;
    private readonly DataGrid _itemsGrid;
    private readonly TextBlock _totalText = new() { FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly ObservableCollection<PurchaseLine> _lines = new();
    private readonly List<ProductDto> _products;

    public CreatePurchaseRequest? Result { get; private set; }

    public PurchaseDialog(List<SupplierDto> suppliers, List<ProductDto> products)
        : base("Registrar compra", "Agregue los artículos comprados; el inventario se actualizará automáticamente.",
            width: 660)
    {
        _products = products.Where(p => p.Active && !p.IsService).ToList();

        var supplierOptions = new List<Option<int?>> { new(null, "Sin proveedor") };
        supplierOptions.AddRange(suppliers.Where(s => s.Active).OrderBy(s => s.Name)
            .Select(s => new Option<int?>(s.Id, s.Name)));
        _supplierBox = AddCombo("Proveedor", supplierOptions);

        _invoiceBox = Add(new TextBox(), "Número de factura del proveedor");
        _notesBox = Add(new TextBox(), "Notas");

        AddSeparator();
        Body.Children.Add(new TextBlock { Text = "ARTÍCULOS", Style = Style("Label"), Margin = new Thickness(0, 4, 0, 8) });

        var productOptions = _products.OrderBy(p => p.Name)
            .Select(p => new Option<ProductDto>(p, $"{p.Name} · stock {p.Stock:N0}")).ToList();

        var addRow = new Grid();
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

        _productBox = new ComboBox
        {
            ItemsSource = productOptions,
            DisplayMemberPath = nameof(Option<object>.DisplayText),
            SelectedIndex = productOptions.Count > 0 ? 0 : -1
        };
        addRow.Children.Add(_productBox);

        _quantityBox = new TextBox { Text = "1" };
        Grid.SetColumn(_quantityBox, 2);
        addRow.Children.Add(_quantityBox);

        _costBox = new TextBox { Text = "0,00" };
        Grid.SetColumn(_costBox, 4);
        addRow.Children.Add(_costBox);

        var addButton = Kit.Button("Agregar", "BlueButton", AddLine);
        addButton.Padding = new Thickness(10, 8, 10, 8);
        Grid.SetColumn(addButton, 6);
        addRow.Children.Add(addButton);

        Body.Children.Add(addRow);

        var hint = new TextBlock
        {
            Text = "Columnas: producto · cantidad · costo unitario (USD).",
            Style = Style("Muted"),
            FontSize = 10,
            Margin = new Thickness(0, 4, 0, 8)
        };
        Body.Children.Add(hint);

        _itemsGrid = Kit.Table(
            Kit.Col("Producto", nameof(PurchaseLine.ProductName)),
            Kit.Col("Cantidad", nameof(PurchaseLine.Quantity), 90, "{0:N2}"),
            Kit.Col("Costo", nameof(PurchaseLine.Cost), 90, "{0:N2}"),
            Kit.Col("Total", nameof(PurchaseLine.Total), 90, "{0:N2}"));
        _itemsGrid.ItemsSource = _lines;
        _itemsGrid.Height = 170;

        var itemsHost = new Grid();
        itemsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        itemsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        itemsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        itemsHost.Children.Add(_itemsGrid);

        var remove = Kit.Button("Quitar", "GhostButton", RemoveLine);
        remove.VerticalAlignment = VerticalAlignment.Top;
        remove.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(remove, 2);
        itemsHost.Children.Add(remove);

        Body.Children.Add(itemsHost);

        var totalRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        totalRow.Children.Add(new TextBlock { Text = "Total de la compra", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        _totalText.Text = "$ 0,00";
        _totalText.FontSize = 16;
        totalRow.Children.Add(_totalText);
        Body.Children.Add(totalRow);

        _lines.CollectionChanged += (_, _) => UpdateTotal();
    }

    private void AddLine()
    {
        if (_productBox.SelectedItem is not Option<ProductDto> option) return;

        if (!decimal.TryParse(_quantityBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var quantity)
            || quantity <= 0)
        {
            ShowError("La cantidad debe ser mayor que cero.");
            return;
        }

        var cost = decimal.TryParse(_costBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var c) ? c : 0m;
        if (cost < 0)
        {
            ShowError("El costo no puede ser negativo.");
            return;
        }

        var existing = _lines.FirstOrDefault(l => l.ProductId == option.Value.Id);
        if (existing is not null)
        {
            existing.Quantity += quantity;
            existing.Cost = cost > 0 ? cost : existing.Cost;
            _itemsGrid.Items.Refresh();
        }
        else
        {
            _lines.Add(new PurchaseLine(option.Value.Id, option.Value.Name, quantity, cost));
        }

        UpdateTotal();
    }

    private void RemoveLine()
    {
        if (_itemsGrid.SelectedItem is PurchaseLine line)
        {
            _lines.Remove(line);
            UpdateTotal();
        }
    }

    private void UpdateTotal()
    {
        _totalText.Text = $"$ {_lines.Sum(l => l.Total):N2}";
    }

    protected override void OnAccept()
    {
        if (_lines.Count == 0)
            throw new InvalidOperationException("Agregue al menos un artículo a la compra.");

        var supplierId = (_supplierBox.SelectedItem as Option<int?>)?.Value;
        var items = _lines.Select(l => new PurchaseItemRequest(l.ProductId, l.Quantity, l.Cost)).ToList();

        Result = new CreatePurchaseRequest(supplierId,
            string.IsNullOrWhiteSpace(_invoiceBox.Text) ? null : _invoiceBox.Text.Trim(),
            string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim(),
            items);

        base.OnAccept();
    }
}

public sealed class PurchaseLine
{
    public PurchaseLine(int productId, string productName, decimal quantity, decimal cost)
    {
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        Cost = cost;
    }

    public int ProductId { get; }
    public string ProductName { get; }
    public decimal Quantity { get; set; }
    public decimal Cost { get; set; }
    public decimal Total => Math.Round(Quantity * Cost, 2);
}

public sealed class PurchaseDetailDialog : FormDialog
{
    public PurchaseDetailDialog(PurchaseDto purchase, List<ProductDto> products)
        : base($"Compra #{purchase.Id}", purchase.DateUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"))
    {
        AcceptButton.Content = "Cerrar";
        HideCancel();

        AddRow("Proveedor", purchase.SupplierName ?? "Sin proveedor");
        AddRow("Factura", purchase.InvoiceNumber ?? "—");
        AddSeparator();

        foreach (var item in purchase.Items)
        {
            var name = products.FirstOrDefault(p => p.Id == item.ProductId)?.Name ?? $"Producto #{item.ProductId}";
            AddRow($"{item.Quantity:N2} × {name}", $"$ {item.Quantity * item.UnitCost:N2}");
        }

        AddSeparator();
        AddRow("Total", $"$ {purchase.Total:N2}", highlight: true);
        if (!string.IsNullOrWhiteSpace(purchase.Notes))
            AddNote(purchase.Notes!);
    }
}

