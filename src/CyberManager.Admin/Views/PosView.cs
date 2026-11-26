using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CyberManager.Admin.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

public sealed class PosView : UserControl, IRefreshable
{
    private readonly WrapPanel _productsPanel = new();
    private readonly StackPanel _cartPanel = new();
    private readonly StackPanel _emptyCart = new();
    private readonly ComboBox _categoryBox = new();
    private readonly TextBox _searchBox = new();
    private readonly ComboBox _customerBox = new();
    private readonly ComboBox _documentTypeBox = new();
    private readonly TextBox _documentNumberBox = Kit.Input();
    private readonly TextBlock _documentPrefix = Kit.Muted("V-");
    private readonly TextBlock _customerConfirmation = Kit.Muted(string.Empty);
    private readonly Button _addCustomerButton;
    private readonly ComboBox _methodBox = new();
    private readonly ComboBox _invoiceBox = new();
    private readonly TextBox _receivedBox = new();
    private readonly TextBox _discountBox = new();
    private readonly TextBox _notesBox = new();
    private readonly TextBlock _rateText = Kit.Muted("Tasa sin definir");
    private readonly TextBlock _subtotalText = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _discountText = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _taxText = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _totalUsdText = new() { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Kit.Brush("AccentBrush"), HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _totalBsText = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _receivedLabel = Kit.Label("Monto recibido (Bs)");
    private readonly TextBlock _changeText = Kit.Muted("Cambio: Bs 0,00");
    private readonly Button _payButton;

    private readonly List<PosLine> _cart = new();
    private List<ProductDto> _products = new();
    private List<CustomerDto> _customers = new();
    private decimal _rate;
    private bool _suspendReceivedUpdate;
    private bool _suspendCustomerApply;
    private bool _suspendDocumentUpdate;
    private bool _autoReceived = true;
    private bool _scannerAutoAdd = true;
    private Dictionary<string, string> _settings = new();

    public PosView()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var headerRight = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        headerRight.Children.Add(_rateText);
        headerRight.Children.Add(Kit.Muted("La caja debe estar abierta para cobrar."));

        root.Children.Add(Kit.PageHeader("Punto de venta",
            "Cobre productos, servicios y tiempo adicional de los clientes.",
            headerRight));

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        Grid.SetRow(content, 1);
        root.Children.Add(content);

        // ================= Catálogo =================
        var catalog = new Grid();
        catalog.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        catalog.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var filters = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _categoryBox.SelectionChanged += (_, _) => RenderProducts();
        filters.Children.Add(_categoryBox);

        _searchBox.Margin = new Thickness(10, 0, 0, 0);
        _searchBox.ToolTip = "Buscar por nombre o código. Con lector: escanee y presione Enter.";
        _searchBox.TextChanged += (_, _) => RenderProducts();
        _searchBox.KeyDown += OnSearchKeyDown;
        Grid.SetColumn(_searchBox, 1);
        filters.Children.Add(_searchBox);

        catalog.Children.Add(filters);

        var catalogScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _productsPanel
        };
        Grid.SetRow(catalogScroll, 1);
        catalog.Children.Add(catalogScroll);

        var catalogCard = Kit.Card(catalog, 14);
        catalogCard.Margin = new Thickness(0, 0, 14, 0);
        content.Children.Add(catalogCard);

        // ================= Ticket =================
        var ticket = new Grid();
        ticket.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ticket.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ticket.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ticket.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ticket.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var ticketHeader = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        ticketHeader.Children.Add(Kit.Heading("Factura", "H2"));
        var ticketActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        ticketActions.Children.Add(Kit.SmallButton("Cancelar ticket", "GhostButton", ClearCart));
        ticketHeader.Children.Add(ticketActions);
        ticket.Children.Add(ticketHeader);

        StackPanel SmallField(string label, FrameworkElement control)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 5) };
            var caption = Kit.Label(label);
            caption.Margin = new Thickness(0, 0, 0, 2);
            panel.Children.Add(caption);
            panel.Children.Add(control);
            return panel;
        }

        _documentTypeBox.ItemsSource = new List<Option<CustomerDocumentType>>
        {
            new(CustomerDocumentType.Personal, "Cédula (V-)"),
            new(CustomerDocumentType.Fiscal, "RIF (J-)")
        };
        _documentTypeBox.DisplayMemberPath = nameof(Option<object>.DisplayText);
        _documentTypeBox.SelectedIndex = 0;

        var customerFields = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        customerFields.Children.Add(SmallField("Cliente", _customerBox));

        _customerConfirmation.Margin = new Thickness(0, 4, 0, 4);
        customerFields.Children.Add(_customerConfirmation);

        var documentRow = new Grid();
        documentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        documentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        documentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var typeField = SmallField("Tipo", _documentTypeBox);
        Grid.SetColumn(typeField, 0);
        documentRow.Children.Add(typeField);

        // El prefijo V-/J- es fijo: el cajero escribe solo el número y el documento
        // completo se guarda con el formato que espera la factura.
        var numberHost = new Grid();
        _documentNumberBox.Padding = new Thickness(34, 0, 6, 0);
        _documentNumberBox.ToolTip = "Escriba solo el número; el prefijo se agrega automáticamente.";
        numberHost.Children.Add(_documentNumberBox);
        _documentPrefix.HorizontalAlignment = HorizontalAlignment.Left;
        _documentPrefix.VerticalAlignment = VerticalAlignment.Center;
        _documentPrefix.Margin = new Thickness(9, 0, 0, 0);
        _documentPrefix.FontWeight = FontWeights.SemiBold;
        _documentPrefix.IsHitTestVisible = false;
        numberHost.Children.Add(_documentPrefix);
        var numberField = SmallField("Número", numberHost);
        Grid.SetColumn(numberField, 2);
        documentRow.Children.Add(numberField);
        customerFields.Children.Add(documentRow);

        _addCustomerButton = Kit.SmallButton("Agregar cliente nuevo", "SuccessButton", AddCustomerFromDocument);
        _addCustomerButton.HorizontalAlignment = HorizontalAlignment.Left;
        _addCustomerButton.Margin = new Thickness(0, 4, 0, 0);
        _addCustomerButton.Visibility = Visibility.Collapsed;
        customerFields.Children.Add(_addCustomerButton);

        _customerBox.SelectionChanged += (_, _) =>
        {
            if (!_suspendCustomerApply) ApplySelectedCustomer();
        };
        _documentTypeBox.SelectionChanged += (_, _) =>
        {
            if (_suspendDocumentUpdate) return;
            UpdateDocumentPrefix();
            SearchCustomerByDocument();
        };
        _documentNumberBox.TextChanged += (_, _) =>
        {
            if (_suspendDocumentUpdate) return;
            SanitizeDocumentNumber();
            SearchCustomerByDocument();
        };
        _documentNumberBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            SearchCustomerByDocument();
            e.Handled = true;
        };
        Grid.SetRow(customerFields, 1);
        ticket.Children.Add(customerFields);

        _emptyCart.Children.Add(new TextBlock
        {
            Text = "Agregue productos del catálogo.",
            Style = Kit.Style("Muted"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 24)
        });
        _emptyCart.Children.Add(new TextBlock
        {
            Text = "El cobro se registra en la caja abierta.",
            Style = Kit.Style("Muted"),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var cartHost = new Grid();
        cartHost.Children.Add(_emptyCart);
        cartHost.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _cartPanel
        });
        Grid.SetRow(cartHost, 2);
        ticket.Children.Add(cartHost);

        // Totales
        var totals = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };
        totals.Children.Add(new Separator { Margin = new Thickness(0, 0, 0, 4) });

        Grid TotalRow(string label, FrameworkElement value)
        {
            var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row.Children.Add(new TextBlock { Text = label, Style = Kit.Style("Muted"), VerticalAlignment = VerticalAlignment.Center });
            value.HorizontalAlignment = HorizontalAlignment.Right;
            (value as FrameworkElement)!.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(value);
            totals.Children.Add(row);
            return row;
        }

        TotalRow("Subtotal", _subtotalText);
        TotalRow("Descuento", _discountText);
        TotalRow("IVA incluido", _taxText);

        totals.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });

        var totalRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        totalRow.Children.Add(new TextBlock { Text = "TOTAL", FontWeight = FontWeights.Bold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center });
        var totalStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
        totalStack.Children.Add(_totalUsdText);
        totalStack.Children.Add(_totalBsText);
        totalRow.Children.Add(totalStack);
        totals.Children.Add(totalRow);

        _changeText.Margin = new Thickness(0, 3, 0, 0);
        _changeText.HorizontalAlignment = HorizontalAlignment.Right;
        totals.Children.Add(_changeText);

        Grid.SetRow(totals, 3);
        ticket.Children.Add(totals);

        // Pago
        var payment = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };

        var paymentRow = new Grid();
        paymentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        paymentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        paymentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var methodField = new StackPanel();
        methodField.Children.Add(Kit.Label("Forma de pago"));
        _methodBox.ItemsSource = new List<Option<PaymentMethod>>
        {
            new(PaymentMethod.Cash, "Efectivo Bs"),
            new(PaymentMethod.CashUsd, "Efectivo USD"),
            new(PaymentMethod.Card, "Punto de venta"),
            new(PaymentMethod.Transfer, "Transferencia"),
            new(PaymentMethod.PagoMovil, "Pago Móvil"),
            new(PaymentMethod.CustomerBalance, "Saldo del cliente")
        };
        _methodBox.DisplayMemberPath = nameof(Option<object>.DisplayText);
        _methodBox.SelectedIndex = 0;
        _methodBox.SelectionChanged += (_, _) => OnMethodChanged();
        methodField.Children.Add(_methodBox);
        paymentRow.Children.Add(methodField);

        var invoiceField = new StackPanel();
        invoiceField.Children.Add(Kit.Label("Tipo de comprobante"));
        _invoiceBox.ItemsSource = new List<Option<InvoiceType>>
        {
            new(InvoiceType.Ticket, "Ticket"),
            new(InvoiceType.Factura, "Factura"),
            new(InvoiceType.NotaEntrega, "Nota de entrega")
        };
        _invoiceBox.DisplayMemberPath = nameof(Option<object>.DisplayText);
        _invoiceBox.SelectedIndex = 0;
        invoiceField.Children.Add(_invoiceBox);
        Grid.SetColumn(invoiceField, 2);
        paymentRow.Children.Add(invoiceField);

        payment.Children.Add(paymentRow);

        var amountRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        amountRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        amountRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        amountRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var receivedField = new StackPanel();
        receivedField.Children.Add(_receivedLabel);
        _receivedBox.Text = "0,00";
        _receivedBox.TextChanged += (_, _) =>
        {
            if (_suspendReceivedUpdate) return;
            _autoReceived = false;
            UpdateTotals();
        };
        receivedField.Children.Add(_receivedBox);
        amountRow.Children.Add(receivedField);

        var discountField = new StackPanel();
        discountField.Children.Add(Kit.Label("Descuento (USD)"));
        _discountBox.Text = "0,00";
        _discountBox.TextChanged += (_, _) => UpdateTotals();
        discountField.Children.Add(_discountBox);
        Grid.SetColumn(discountField, 2);
        amountRow.Children.Add(discountField);

        payment.Children.Add(amountRow);

        payment.Children.Add(Kit.Field("Notas del ticket", _notesBox));

        _payButton = Kit.Button("Terminar y cobrar", "SuccessButton", () => _ = PayAsync());
        _payButton.Padding = new Thickness(12, 9, 12, 9);
        _payButton.FontSize = 14;
        _payButton.Margin = new Thickness(0, 6, 0, 0);
        payment.Children.Add(_payButton);

        Grid.SetRow(payment, 4);
        ticket.Children.Add(payment);

        var ticketCard = Kit.Card(ticket, 12);
        Grid.SetColumn(ticketCard, 1);
        content.Children.Add(ticketCard);

        Content = root;

        Loaded += (_, _) =>
        {
            _searchBox.Focus();
            AppearanceService.Changed -= OnAppearanceChanged;
            AppearanceService.Changed += OnAppearanceChanged;
        };
        Unloaded += (_, _) => AppearanceService.Changed -= OnAppearanceChanged;
    }

    /// <summary>Repinta el catálogo cuando cambia el modo de imagen o la escala de las tarjetas.</summary>
    private void OnAppearanceChanged(AppearanceSettings settings)
        => Dispatcher.Invoke(RenderProducts);

    /// <summary>El lector de código de barras escribe el código y envía Enter.</summary>
    private void OnSearchKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return)) return;

        var term = _searchBox.Text.Trim();
        if (term.Length == 0) return;

        e.Handled = true;
        if (!_scannerAutoAdd) return;

        var product = _products.FirstOrDefault(p => p.Active &&
                          string.Equals(p.Sku, term, StringComparison.OrdinalIgnoreCase))
                      ?? _products.FirstOrDefault(p => p.Active &&
                          string.Equals(p.Name, term, StringComparison.OrdinalIgnoreCase));

        if (product is null)
        {
            Ui.Info($"No se encontró un producto con el código o nombre «{term}».", "Lector de código");
            _searchBox.SelectAll();
            return;
        }

        AddToCart(product);
        _searchBox.Clear();
        _searchBox.Focus();
    }

    public async Task RefreshAsync()
    {
        var bootstrap = await App.Api.BootstrapAsync();
        _settings = bootstrap.Settings;
        _products = bootstrap.Products.Where(p => p.Active).ToList();
        _rate = bootstrap.ExchangeRate?.Rate ?? 0;

        if (bootstrap.Settings.TryGetValue("ScannerAutoAdd", out var scanner))
            _scannerAutoAdd = !bool.TryParse(scanner, out var add) || add;

        _customers = await App.Api.CustomersAsync();

        _rateText.Text = _rate > 0
            ? $"Tasa del día: 1 USD = {_rate:N2} Bs"
            : "Registre la tasa del día en Configuración para poder cobrar.";
        _rateText.Foreground = _rate > 0 ? Kit.Brush("AccentBrush") : Kit.Brush("DangerBrush");

        var previousCategory = (_categoryBox.SelectedItem as Option<int?>)?.Value;
        var categoryOptions = new List<Option<int?>>
        {
            new(null, "Todas las categorías")
        };
        categoryOptions.AddRange(_products
            .Where(p => p.CategoryId is not null)
            .GroupBy(p => new { p.CategoryId, p.CategoryName })
            .Select(g => new Option<int?>(g.Key.CategoryId, g.Key.CategoryName ?? "General"))
            .OrderBy(o => o.Text));

        _categoryBox.ItemsSource = categoryOptions;
        _categoryBox.DisplayMemberPath = nameof(Option<object>.DisplayText);
        _categoryBox.SelectedIndex = categoryOptions.FindIndex(o => o.Value == previousCategory);
        if (_categoryBox.SelectedIndex < 0) _categoryBox.SelectedIndex = 0;

        var previousCustomer = (_customerBox.SelectedItem as Option<int?>)?.Value;
        var customerOptions = new List<Option<int?>>
        {
            new(null, "Cliente ocasional")
        };
        customerOptions.AddRange(_customers
            .OrderBy(c => c.Name)
            .Select(c => new Option<int?>(c.Id, c.Name)));

        // Una actualización en segundo plano no debe borrar los datos escritos
        // manualmente para un cliente ocasional.
        _suspendCustomerApply = true;
        try
        {
            _customerBox.ItemsSource = customerOptions;
            _customerBox.DisplayMemberPath = nameof(Option<object>.DisplayText);
            _customerBox.SelectedIndex = Math.Max(0, customerOptions.FindIndex(o => o.Value == previousCustomer));
        }
        finally
        {
            _suspendCustomerApply = false;
        }

        var currentCustomer = (_customerBox.SelectedItem as Option<int?>)?.Value;
        if (currentCustomer != previousCustomer)
            ApplySelectedCustomer();

        RenderProducts();
        UpdateTotals();
    }

    private void RenderProducts()
    {
        _productsPanel.Children.Clear();

        var category = (_categoryBox.SelectedItem as Option<int?>)?.Value;
        var term = _searchBox.Text.Trim();

        var filtered = _products
            .Where(p => category is null || p.CategoryId == category)
            .Where(p => term.Length == 0
                        || p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || p.Sku.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name)
            .ToList();

        if (filtered.Count == 0)
        {
            _productsPanel.Children.Add(new TextBlock
            {
                Text = "No hay productos que coincidan con la búsqueda.",
                Style = Kit.Style("Muted"),
                Margin = new Thickness(6)
            });
            return;
        }

        foreach (var product in filtered)
            _productsPanel.Children.Add(ProductTile(product));
    }

    private Border ProductTile(ProductDto product)
    {
        var available = product.IsService || product.Stock > 0;
        var scale = AppearanceService.Current.ProductCardScale / 100.0;
        var stockText = product.IsService ? "Servicio" : product.Stock <= 0 ? "Agotado" : $"{product.Stock:N0} disp.";

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = product.Name,
            FontSize = 13 * scale,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 38 * scale,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        content.Children.Add(new TextBlock
        {
            Text = product.CategoryName ?? "General",
            Style = Kit.Style("Muted"),
            FontSize = 11 * scale,
            Margin = new Thickness(0, 2 * scale, 0, 0)
        });
        content.Children.Add(new TextBlock
        {
            Text = $"$ {product.Price:N2}",
            FontSize = 13 * scale,
            FontWeight = FontWeights.Bold,
            Foreground = Kit.Brush("AccentBrush"),
            Margin = new Thickness(0, 4 * scale, 0, 0)
        });
        layout.Children.Add(content);

        var foot = new Grid();
        foot.Children.Add(new TextBlock
        {
            Text = stockText,
            Style = Kit.Style("Muted"),
            FontSize = 10 * scale,
            VerticalAlignment = VerticalAlignment.Bottom
        });
        var bs = new TextBlock
        {
            Text = _rate > 0 ? $"Bs {product.Price * _rate:N2}" : string.Empty,
            Style = Kit.Style("Muted"),
            FontSize = 10 * scale,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        foot.Children.Add(bs);
        Grid.SetRow(foot, 1);
        layout.Children.Add(foot);

        var tile = new Border
        {
            Width = Math.Round(184 * scale),
            Height = Math.Round(108 * scale),
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(Math.Round(12 * scale)),
            CornerRadius = new CornerRadius(10 * scale),
            Background = Kit.Brush("SurfaceAltBrush"),
            BorderBrush = Kit.Brush("BorderBrush"),
            BorderThickness = new Thickness(1),
            Cursor = available ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
            Opacity = available ? 1 : 0.45,
            ToolTip = product.Name,
            DataContext = product,
            Child = layout
        };

        if (available)
            tile.MouseLeftButtonUp += (_, _) => AddToCart(product);

        return tile;
    }

    private void AddToCart(ProductDto product)
    {
        var line = _cart.FirstOrDefault(l => l.Product.Id == product.Id);
        if (line is null)
        {
            _cart.Add(new PosLine(product));
        }
        else
        {
            var max = product.IsService ? 9999 : product.Stock;
            if (line.Quantity + 1 > max)
            {
                Ui.Info($"Solo hay {product.Stock:N0} unidades de {product.Name}.", "Stock insuficiente");
                return;
            }
            line.Quantity += 1;
        }

        RenderCart();
        UpdateTotals();
    }

    private void RenderCart()
    {
        _cartPanel.Children.Clear();
        var hasItems = _cart.Count > 0;
        _emptyCart.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;

        foreach (var line in _cart.ToList())
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new TextBlock
            {
                Text = $"{line.Quantity:0.##} × {line.Product.Name}",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = $"{line.Product.Name} · $ {line.Product.Price:N2} c/u",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });

            var total = new TextBlock
            {
                Text = $"$ {line.Total:N2}",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Kit.Brush("AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(total, 1);
            row.Children.Add(total);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var edit = Kit.SmallButton("Editar", "GhostButton", () => EditLine(line));
            edit.FontSize = 11;
            edit.Padding = new Thickness(5, 2, 5, 2);
            edit.Margin = new Thickness(0, 0, 2, 0);
            actions.Children.Add(edit);

            var remove = Kit.SmallButton("✕", "IconButton", () => RemoveLine(line));
            remove.Foreground = Kit.Brush("DangerBrush");
            remove.Width = 22;
            remove.Height = 22;
            remove.Padding = new Thickness(0);
            remove.ToolTip = $"Eliminar {line.Product.Name} del ticket";
            actions.Children.Add(remove);

            Grid.SetColumn(actions, 2);
            row.Children.Add(actions);
            _cartPanel.Children.Add(row);
        }
    }

    private void RemoveLine(PosLine line)
    {
        _cart.Remove(line);
        RenderCart();
        UpdateTotals();
    }

    private void EditLine(PosLine line)
    {
        var usedIds = _cart.Where(l => l != line).Select(l => l.Product.Id).ToHashSet();
        var dialog = new EditCartLineDialog(_products, line.Product, line.Quantity, usedIds)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true) return;

        line.Product = _products.First(p => p.Id == dialog.ProductId);
        line.Quantity = dialog.Quantity;
        RenderCart();
        UpdateTotals();
    }

    private void ClearCart()
    {
        if (_cart.Count == 0) return;
        if (!Ui.Confirm("¿Vaciar el ticket actual?")) return;
        _cart.Clear();
        _discountBox.Text = "0,00";
        _notesBox.Text = string.Empty;
        RenderCart();
        UpdateTotals();
    }

    private PaymentMethod SelectedMethod =>
        (_methodBox.SelectedItem as Option<PaymentMethod>)?.Value ?? PaymentMethod.Cash;

    private bool PaysUsd => SelectedMethod == PaymentMethod.CashUsd;

    private decimal Gross => _cart.Sum(l => l.Total);
    private decimal Discount => ParseMoney(_discountBox.Text);
    private decimal FinalAmount => Math.Max(0, Math.Round(Gross - Discount, 2));
    private decimal GrandTotal => FinalAmount;
    private decimal Due => PaysUsd ? GrandTotal : Math.Round(GrandTotal * _rate, 2);

    private static decimal ParseMoney(string? text)
        => decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var value)
            ? Math.Max(0, value)
            : 0m;

    private static string? NullIfEmpty(string text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private CustomerDocumentType SelectedDocumentType =>
        (_documentTypeBox.SelectedItem as Option<CustomerDocumentType>)?.Value ?? CustomerDocumentType.Personal;

    private string DocumentPrefix => SelectedDocumentType == CustomerDocumentType.Fiscal ? "J-" : "V-";

    private string? CurrentDocumentNumber
    {
        get
        {
            var digits = _documentNumberBox.Text.Trim();
            return digits.Length == 0 ? null : DocumentPrefix + digits;
        }
    }

    private void UpdateDocumentPrefix() => _documentPrefix.Text = DocumentPrefix;

    private void SanitizeDocumentNumber()
    {
        var digits = new string(_documentNumberBox.Text.Where(char.IsDigit).ToArray());
        if (digits == _documentNumberBox.Text) return;
        _suspendDocumentUpdate = true;
        try
        {
            _documentNumberBox.Text = digits;
            _documentNumberBox.CaretIndex = digits.Length;
        }
        finally { _suspendDocumentUpdate = false; }
    }

    private void SetDocumentNumber(string? full)
    {
        var value = (full ?? string.Empty).Trim();
        var type = CustomerDocumentType.Personal;
        if (value.StartsWith("J-", StringComparison.OrdinalIgnoreCase))
        {
            type = CustomerDocumentType.Fiscal;
            value = value[2..];
        }
        else if (value.StartsWith("V-", StringComparison.OrdinalIgnoreCase)) value = value[2..];

        _suspendDocumentUpdate = true;
        try
        {
            _documentTypeBox.SelectedIndex = type == CustomerDocumentType.Fiscal ? 1 : 0;
            _documentNumberBox.Text = value;
        }
        finally { _suspendDocumentUpdate = false; }
        UpdateDocumentPrefix();
    }

    private void ShowCustomerConfirmation(CustomerDto? customer, bool showAddButton)
    {
        if (customer is not null)
        {
            _customerConfirmation.Text = $"Cliente confirmado: {customer.Name}";
            _customerConfirmation.Foreground = Kit.Brush("SuccessBrush");
            _addCustomerButton.Visibility = Visibility.Collapsed;
            return;
        }

        if (showAddButton)
        {
            _customerConfirmation.Text = "Cliente no registrado. Agréguelo al sistema para continuar.";
            _customerConfirmation.Foreground = Kit.Brush("WarningBrush");
            _addCustomerButton.Visibility = Visibility.Visible;
            return;
        }

        _customerConfirmation.Text = string.Empty;
        _addCustomerButton.Visibility = Visibility.Collapsed;
    }

    private void SearchCustomerByDocument()
    {
        var full = CurrentDocumentNumber;
        if (full is null || full.Length <= 3)
        {
            _suspendCustomerApply = true;
            try { _customerBox.SelectedIndex = 0; } finally { _suspendCustomerApply = false; }
            ShowCustomerConfirmation(null, false);
            return;
        }

        var match = _customers.FirstOrDefault(c =>
            string.Equals(c.DocumentId, full, StringComparison.OrdinalIgnoreCase));

        var options = _customerBox.ItemsSource as IList<Option<int?>>;
        var option = match is null ? null : options?.FirstOrDefault(o => o.Value == match.Id);
        _suspendCustomerApply = true;
        try { _customerBox.SelectedIndex = option is null ? 0 : options!.IndexOf(option); }
        finally { _suspendCustomerApply = false; }

        ShowCustomerConfirmation(match, match is null);
    }

    private void ApplySelectedCustomer()
    {
        if (_customerBox.SelectedItem is Option<int?> { Value: int customerId })
        {
            var customer = _customers.FirstOrDefault(c => c.Id == customerId);
            if (customer is not null)
            {
                SetDocumentNumber(customer.DocumentId);
                ShowCustomerConfirmation(customer, false);
                return;
            }
        }

        ShowCustomerConfirmation(null, false);
    }

    private void AddCustomerFromDocument()
    {
        var full = CurrentDocumentNumber;
        if (full is null) return;

        var dialog = new QuickCustomerDialog(full, SelectedDocumentType) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveCustomerAsync(null, dialog.Result);
            await RefreshAsync();
            SetDocumentNumber(full);
            SearchCustomerByDocument();
        }, "No se pudo agregar el cliente");
    }

    private void OnMethodChanged()
    {
        _autoReceived = true;
        UpdateTotals();
    }

    private void UpdateTotals()
    {
        var gross = Gross;
        var final = FinalAmount;
        var tax = _cart.Sum(l => Math.Round(l.Total * l.Product.TaxRate / (100m + l.Product.TaxRate), 2));
        if (gross > 0) tax = Math.Round(tax * final / gross, 2);

        _subtotalText.Text = $"$ {gross:N2}";
        _discountText.Text = $"- $ {Discount:N2}";
        _taxText.Text = $"$ {tax:N2}";

        _totalUsdText.Text = $"$ {GrandTotal:N2}";
        _totalBsText.Text = _rate > 0 ? $"Bs {Math.Round(GrandTotal * _rate, 2):N2}" : "Tasa sin definir";

        _receivedLabel.Text = PaysUsd ? "Monto recibido (USD)" : "Monto recibido (Bs)";
        if (_autoReceived)
        {
            _suspendReceivedUpdate = true;
            _receivedBox.Text = Due.ToString("N2", CultureInfo.CurrentCulture);
            _suspendReceivedUpdate = false;
        }

        var received = ParseMoney(_receivedBox.Text);
        var change = Math.Max(0, Math.Round(received - Due, 2));
        _changeText.Text = PaysUsd ? $"Cambio: $ {change:N2}" : $"Cambio: Bs {change:N2}";

        _payButton.Content = _cart.Count == 0
            ? "Terminar y cobrar"
            : $"Terminar y cobrar · {Due:N2} {(PaysUsd ? "USD" : "Bs")}";
        _payButton.IsEnabled = _cart.Count > 0 && _rate > 0;
    }

    private async Task PayAsync()
    {
        if (_cart.Count == 0) return;

        if (_rate <= 0)
        {
            Ui.Error("Debe registrar la tasa de cambio del día antes de vender.", "Tasa sin definir");
            return;
        }

        var method = SelectedMethod;
        var customerId = (_customerBox.SelectedItem as Option<int?>)?.Value;
        var customer = _customers.FirstOrDefault(c => c.Id == customerId);
        var customerName = customer?.Name;
        var documentNumber = CurrentDocumentNumber;
        var documentType = SelectedDocumentType;

        if (documentNumber is not null && customer is null)
        {
            Ui.Error("El documento no está registrado. Presione «Agregar cliente nuevo» antes de cobrar.",
                "Cliente requerido");
            return;
        }

        if (method == PaymentMethod.CustomerBalance && customerId is null)
        {
            Ui.Error("Seleccione el cliente al que se cargará el consumo.", "Cliente requerido");
            return;
        }

        var invoice = (_invoiceBox.SelectedItem as Option<InvoiceType>)?.Value ?? InvoiceType.Ticket;
        var received = ParseMoney(_receivedBox.Text);
        if (received <= 0) received = Due;

        var items = _cart.Select(l => new SaleItemRequest(
            l.Product.Id, l.Product.Name, l.Quantity, l.Product.Price, 0, l.Product.TaxRate)).ToList();

        var notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim();
        var request = new CreateSaleRequest(
            items, method, received, Discount, customerId, null, null,
            notes, invoice,
            PaysUsd ? Currency.Usd : Currency.Ves,
            null, documentNumber, customerName, documentType);

        var tax = _cart.Sum(l => Math.Round(l.Total * l.Product.TaxRate / (100m + l.Product.TaxRate), 2));
        if (Gross > 0) tax = Math.Round(tax * GrandTotal / Gross, 2);

        // Comprobante provisional: todavía no se envía nada al servidor.
        var draft = new SaleDto(
            0, "PENDIENTE", null, null, null,
            customerId,
            customerName,
            documentNumber,
            Gross, Discount, tax, GrandTotal, _rate, Math.Round(GrandTotal * _rate, 2),
            received, PaysUsd ? Currency.Usd : Currency.Ves,
            Math.Max(0, Math.Round(received - Due, 2)), method, invoice, null,
            SaleStatus.Completed, DateTime.UtcNow, notes,
            _cart.Select(l => new SaleItemDto(0, l.Product.Id, l.Product.Name, l.Quantity,
                l.Product.Price, 0, l.Total)).ToList(),
            documentType);

        var ticketSettings = TicketSettings.From(_settings);

        async Task RegisterAsync()
        {
            _payButton.IsEnabled = false;
            _payButton.Content = "Procesando...";
            try
            {
                var sale = await App.Api.CreateSaleAsync(request);
                if (ticketSettings.AutoPrint && !string.IsNullOrWhiteSpace(ticketSettings.PrinterName))
                {
                    try { TicketPrinter.PrintSale(sale, ticketSettings, silent: true); }
                    catch { /* La venta ya quedó registrada; se puede reimprimir. */ }
                }
            }
            finally
            {
                UpdateTotals();
            }
        }

        try
        {
            var dialog = new SaleReceiptDialog(draft, ticketSettings, RegisterAsync)
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();

            // «Ir atrás» deja DialogResult en null: el ticket sigue editable y no se registra.
            if (dialog.DialogResult != true) return;

            _cart.Clear();
            _suspendCustomerApply = true;
            try { _customerBox.SelectedIndex = 0; } finally { _suspendCustomerApply = false; }
            SetDocumentNumber(null);
            ShowCustomerConfirmation(null, false);
            _discountBox.Text = "0,00";
            _notesBox.Text = string.Empty;
            _autoReceived = true;
            RenderCart();
            _searchBox.Focus();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Ui.Error(ex.Message, "No se pudo registrar la venta");
        }
        finally
        {
            UpdateTotals();
        }
    }
}

/// <summary>Alta rápida de cliente desde el POS, usando el documento ya escrito.</summary>
public sealed class QuickCustomerDialog : FormDialog
{
    private readonly TextBox _name;
    private readonly string _documentNumber;
    private readonly CustomerDocumentType _documentType;

    public SaveCustomerRequest? Result { get; private set; }

    public QuickCustomerDialog(string documentNumber, CustomerDocumentType documentType)
        : base("Agregar cliente nuevo", "El cliente quedará registrado y se usará en la factura.")
    {
        _documentNumber = documentNumber;
        _documentType = documentType;
        AddRow("Documento", documentNumber);
        AddRow("Tipo", documentType == CustomerDocumentType.Fiscal ? "RIF (J-)" : "Cédula (V-)");
        AddSeparator();
        _name = Add(new TextBox(), "Nombre o razón social");
    }

    protected override void OnAccept()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
            throw new InvalidOperationException("Indique el nombre del cliente.");

        Result = new SaveCustomerRequest(
            _name.Text.Trim(), null, null, _documentNumber, null, null,
            0, false, true, null, _documentType);
        base.OnAccept();
    }
}

public sealed class PosLine
{
    public PosLine(ProductDto product) => Product = product;
    public ProductDto Product { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal Total => Math.Round(Product.Price * Quantity, 2);
}

/// <summary>Edita el producto y la cantidad de una línea antes de emitir el ticket.</summary>
public sealed class EditCartLineDialog : FormDialog
{
    private readonly ComboBox _productBox;
    private readonly TextBox _quantityBox;
    private readonly List<ProductDto> _products;

    public int ProductId { get; private set; }
    public decimal Quantity { get; private set; }

    public EditCartLineDialog(IEnumerable<ProductDto> products, ProductDto current,
        decimal quantity, HashSet<int> usedIds)
        : base("Editar producto del ticket",
            "Cambie el producto o la cantidad antes de emitir el ticket.", "Guardar", 420)
    {
        _products = products
            .Where(p => p.Id == current.Id || (!usedIds.Contains(p.Id) && (p.IsService || p.Stock > 0)))
            .OrderBy(p => p.Name)
            .ToList();

        _productBox = AddCombo("Producto", _products.Select(p => new Option<int>(p.Id, p.Name)));
        var index = _products.FindIndex(p => p.Id == current.Id);
        if (index >= 0) _productBox.SelectedIndex = index;

        _quantityBox = new TextBox { Text = quantity.ToString("0.##", CultureInfo.CurrentCulture) };
        Add(_quantityBox, "Cantidad");
    }

    protected override void OnAccept()
    {
        if (_productBox.SelectedItem is not Option<int> selected)
        {
            ShowError("Seleccione un producto.");
            return;
        }

        if (!decimal.TryParse(_quantityBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture,
                out var quantity) || quantity <= 0)
        {
            ShowError("La cantidad debe ser mayor que cero.");
            return;
        }

        var product = _products.FirstOrDefault(p => p.Id == selected.Value);
        if (product is null)
        {
            ShowError("El producto ya no está disponible.");
            return;
        }

        if (!product.IsService && quantity > product.Stock)
        {
            ShowError($"Solo hay {product.Stock:N0} unidades disponibles de {product.Name}.");
            return;
        }

        ProductId = selected.Value;
        Quantity = Math.Round(quantity, 2);
        base.OnAccept();
    }
}

public sealed class SaleReceiptDialog : FormDialog
{
    private readonly Func<Task>? _onAccept;

    /// <summary>
    /// Muestra el comprobante. Si se indica <paramref name="onAccept"/>, la venta no se registra
    /// hasta que el usuario presiona «Aceptar»; «Ir atrás» cierra sin tocar el ticket.
    /// </summary>
    public SaleReceiptDialog(SaleDto sale, TicketSettings? ticketSettings = null, Func<Task>? onAccept = null)
        : base(onAccept is null ? $"Venta {sale.Number}" : "Confirmar venta",
            $"{Labels.InvoiceName(sale.InvoiceType)} · {Labels.PaymentName(sale.Method)}")
    {
        _onAccept = onAccept;
        AcceptButton.Content = "Aceptar";
        HideCancel();

        var backButton = Kit.Button("Ir atrás", "GhostButton", Close);
        if (AcceptButton.Parent is Panel actions)
        {
            backButton.Margin = new Thickness(0, 0, 8, 0);
            actions.Children.Insert(actions.Children.IndexOf(AcceptButton), backButton);
        }

        AddRow("Fecha", sale.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        if (!string.IsNullOrWhiteSpace(sale.ControlNumber))
            AddRow("N° de control", sale.ControlNumber!);
        if (!string.IsNullOrWhiteSpace(sale.CustomerName))
            AddRow("Cliente", sale.CustomerName!);
        if (!string.IsNullOrWhiteSpace(sale.CustomerTaxId))
        {
            var documentLabel = sale.CustomerDocumentType is CustomerDocumentType type
                ? Labels.DocumentTypeName(type)
                : "Documento / RIF";
            AddRow(documentLabel, sale.CustomerTaxId!);
        }

        AddSeparator();

        foreach (var item in sale.Items)
            AddRow($"{item.Quantity:0.##} × {item.Description}", $"$ {item.Total:N2}");

        AddSeparator();
        AddRow("Subtotal", $"$ {sale.Subtotal:N2}");
        if (sale.Discount > 0) AddRow("Descuento", $"- $ {sale.Discount:N2}");
        AddRow("IVA", $"$ {sale.Tax:N2}");
        AddRow("Total USD", $"$ {sale.Total:N2}", highlight: true);
        AddRow("Total Bs", $"Bs {sale.TotalVes:N2}");
        AddRow("Tasa aplicada", $"1 USD = {sale.ExchangeRate:N2} Bs");

        AddSeparator();
        AddRow("Recibido", sale.PaidCurrency == Currency.Usd ? $"$ {sale.PaidAmount:N2}" : $"Bs {sale.PaidAmount:N2}");
        if (sale.Change > 0)
            AddRow("Cambio", sale.PaidCurrency == Currency.Usd ? $"$ {sale.Change:N2}" : $"Bs {sale.Change:N2}");

        bool TryPrint()
        {
            try
            {
                var settings = ticketSettings ?? TicketSettings.From(new Dictionary<string, string>());
                if (TicketPrinter.PrintSale(sale, settings))
                    return true;

                Ui.Info("No se seleccionó una impresora.", "Impresión cancelada");
            }
            catch (Exception ex)
            {
                Ui.Error(ex.Message, "No se pudo imprimir el ticket");
            }

            return false;
        }

        if (_onAccept is null)
        {
            var print = Kit.Button("Imprimir ticket", "BlueButton", () => TryPrint());
            var printAndAccept = Kit.Button("Imprimir y aceptar", "PrimaryButton", () =>
            {
                if (TryPrint()) DialogResult = true;
            });

            var printActions = Kit.HStack(8, print, printAndAccept);
            printActions.HorizontalAlignment = HorizontalAlignment.Left;
            printActions.Margin = new Thickness(0, 12, 0, 0);
            Body.Children.Add(printActions);
        }

        AddNote(_onAccept is not null
            ? "La venta se registrará al presionar «Aceptar». «Ir atrás» conserva el ticket para editarlo."
            : sale.Status == SaleStatus.Cancelled
                ? "Esta venta está anulada."
                : "Venta registrada en la caja del turno. Use «Aceptar» para cerrar el mensaje.",
            _onAccept is not null || sale.Status == SaleStatus.Cancelled ? "MutedBrush" : "SuccessBrush");
    }

    protected override async void OnAccept()
    {
        if (_onAccept is null)
        {
            base.OnAccept();
            return;
        }

        AcceptButton.IsEnabled = false;
        try
        {
            await _onAccept();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            AcceptButton.IsEnabled = true;
        }
    }
}

