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
    private readonly ComboBox _methodBox = new();
    private readonly ComboBox _invoiceBox = new();
    private readonly TextBox _receivedBox = new();
    private readonly TextBox _discountBox = new();
    private readonly TextBox _notesBox = new();
    private readonly TextBlock _rateText = Kit.Muted("Tasa sin definir");
    private readonly TextBlock _subtotalText = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _discountText = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _taxText = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _igtfText = new() { HorizontalAlignment = HorizontalAlignment.Right, Visibility = Visibility.Collapsed };
    private readonly TextBlock _igtfLabel = Kit.Muted("IGTF 3%");
    private readonly TextBlock _totalUsdText = new() { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Kit.Brush("AccentBrush"), HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _totalBsText = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _receivedLabel = Kit.Label("Monto recibido (Bs)");
    private readonly TextBlock _changeText = Kit.Muted("Cambio: Bs 0,00");
    private readonly Button _payButton;

    private readonly List<PosLine> _cart = new();
    private List<ProductDto> _products = new();
    private List<CustomerDto> _customers = new();
    private decimal _rate;
    private decimal _igtfRate = 3m;
    private bool _igtfEnabled = true;
    private bool _suspendReceivedUpdate;
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
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(392) });
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

        var ticketHeader = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        ticketHeader.Children.Add(Kit.Heading("Ticket de venta", "H2"));
        var clear = Kit.SmallButton("Vaciar", "GhostButton", ClearCart);
        clear.HorizontalAlignment = HorizontalAlignment.Right;
        ticketHeader.Children.Add(clear);
        ticket.Children.Add(ticketHeader);

        _customerBox.Margin = new Thickness(0, 0, 0, 8);
        Grid.SetRow(_customerBox, 1);
        ticket.Children.Add(_customerBox);

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
        var totals = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        totals.Children.Add(new Separator());

        Grid TotalRow(string label, FrameworkElement value)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
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
        var igtfRow = TotalRow("IGTF", _igtfText);
        _igtfLabel.Visibility = Visibility.Collapsed;
        igtfRow.Children.Clear();
        igtfRow.Children.Add(_igtfLabel);
        igtfRow.Children.Add(_igtfText);

        totals.Children.Add(new Separator());

        var totalRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        totalRow.Children.Add(new TextBlock { Text = "TOTAL", FontWeight = FontWeights.Bold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center });
        var totalStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
        totalStack.Children.Add(_totalUsdText);
        totalStack.Children.Add(_totalBsText);
        totalRow.Children.Add(totalStack);
        totals.Children.Add(totalRow);

        _changeText.Margin = new Thickness(0, 6, 0, 0);
        _changeText.HorizontalAlignment = HorizontalAlignment.Right;
        totals.Children.Add(_changeText);

        Grid.SetRow(totals, 3);
        ticket.Children.Add(totals);

        // Pago
        var payment = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

        var paymentRow = new Grid();
        paymentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        paymentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        paymentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var methodField = new StackPanel();
        methodField.Children.Add(Kit.Label("Forma de pago"));
        _methodBox.ItemsSource = new List<Option<PaymentMethod>>
        {
            new(PaymentMethod.Cash, "Efectivo Bs"),
            new(PaymentMethod.CashUsd, "Efectivo USD (IGTF)"),
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
        invoiceField.Children.Add(Kit.Label("Documento"));
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

        var amountRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
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

        _payButton = Kit.Button("Cobrar", "SuccessButton", () => _ = PayAsync());
        _payButton.Padding = new Thickness(14, 12, 14, 12);
        _payButton.FontSize = 14;
        _payButton.Margin = new Thickness(0, 4, 0, 0);
        payment.Children.Add(_payButton);

        Grid.SetRow(payment, 4);
        ticket.Children.Add(payment);

        var ticketCard = Kit.Card(ticket, 16);
        Grid.SetColumn(ticketCard, 1);
        content.Children.Add(ticketCard);

        Content = root;

        Loaded += (_, _) => _searchBox.Focus();
    }

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

        if (bootstrap.Settings.TryGetValue("IgtfEnabled", out var igtfEnabled))
            _igtfEnabled = bool.TryParse(igtfEnabled, out var enabled) ? enabled : true;
        if (bootstrap.Settings.TryGetValue("IgtfRate", out var igtf))
            _igtfRate = decimal.TryParse(igtf, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 3m;
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

        _customerBox.ItemsSource = customerOptions;
        _customerBox.DisplayMemberPath = nameof(Option<object>.DisplayText);
        _customerBox.SelectedIndex = Math.Max(0, customerOptions.FindIndex(o => o.Value == previousCustomer));

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

        var content = new StackPanel();

        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var name = new TextBlock
        {
            Text = product.Name,
            FontSize = 13 * scale,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 38 * scale,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        if (!string.IsNullOrWhiteSpace(product.Icon))
        {
            head.Children.Add(new Border
            {
                Width = Math.Round(30 * scale),
                Height = Math.Round(30 * scale),
                CornerRadius = new CornerRadius(8 * scale),
                Background = Kit.Brush("SurfaceBrush"),
                Margin = new Thickness(0, 0, 8 * scale, 0),
                Child = new TextBlock
                {
                    Text = product.Icon,
                    FontSize = Math.Round(17 * scale),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
            Grid.SetColumn(name, 1);
        }

        head.Children.Add(name);
        content.Children.Add(head);
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
            DataContext = product
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(content);

        var foot = new Grid();
        foot.Children.Add(new TextBlock
        {
            Text = product.IsService ? "Servicio" : product.Stock <= 0 ? "Agotado" : $"{product.Stock:N0} disp.",
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

        tile.Child = layout;

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
            var panel = new Border
            {
                Style = Kit.Style("Panel"),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(new TextBlock
            {
                Text = line.Product.Name,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 8, 0)
            });

            var total = new TextBlock
            {
                Text = $"$ {line.Total:N2}",
                FontWeight = FontWeights.SemiBold,
                Foreground = Kit.Brush("AccentBrush"),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(total, 1);
            grid.Children.Add(total);

            var controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0)
            };
            controls.Children.Add(new TextBlock
            {
                Text = $"$ {line.Product.Price:N2} c/u",
                Style = Kit.Style("Muted"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            var minus = Kit.SmallButton("−", "IconButton", () => ChangeQuantity(line, -1));
            minus.Width = 26;
            minus.Height = 26;
            minus.Padding = new Thickness(0);
            controls.Children.Add(minus);

            controls.Children.Add(new TextBlock
            {
                Text = line.Quantity.ToString("0.##"),
                Width = 38,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            });

            var plus = Kit.SmallButton("+", "IconButton", () => ChangeQuantity(line, 1));
            plus.Width = 26;
            plus.Height = 26;
            plus.Padding = new Thickness(0);
            controls.Children.Add(plus);

            var remove = Kit.SmallButton("✕", "IconButton", () =>
            {
                _cart.Remove(line);
                RenderCart();
                UpdateTotals();
            });
            remove.Foreground = Kit.Brush("DangerBrush");
            remove.Width = 26;
            remove.Height = 26;
            remove.Padding = new Thickness(0);
            remove.Margin = new Thickness(6, 0, 0, 0);
            controls.Children.Add(remove);

            Grid.SetRow(controls, 1);
            Grid.SetColumnSpan(controls, 2);
            grid.Children.Add(controls);

            panel.Child = grid;
            _cartPanel.Children.Add(panel);
        }
    }

    private void ChangeQuantity(PosLine line, int delta)
    {
        if (delta > 0 && !line.Product.IsService && line.Quantity + 1 > line.Product.Stock)
        {
            Ui.Info($"Solo hay {line.Product.Stock:N0} unidades de {line.Product.Name}.", "Stock insuficiente");
            return;
        }

        line.Quantity += delta;
        if (line.Quantity <= 0) _cart.Remove(line);

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
    private decimal Igtf => PaysUsd && _igtfEnabled
        ? Math.Round(FinalAmount * _igtfRate / 100m, 2)
        : 0m;
    private decimal GrandTotal => Math.Round(FinalAmount + Igtf, 2);
    private decimal Due => PaysUsd ? GrandTotal : Math.Round(GrandTotal * _rate, 2);

    private static decimal ParseMoney(string? text)
        => decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var value)
            ? Math.Max(0, value)
            : 0m;

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
        _igtfText.Text = $"$ {Igtf:N2}";
        _igtfText.Visibility = PaysUsd && _igtfEnabled ? Visibility.Visible : Visibility.Collapsed;
        _igtfLabel.Visibility = _igtfText.Visibility;

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

        _payButton.Content = _cart.Count == 0 ? "Cobrar" : $"Cobrar {Due:N2} {(PaysUsd ? "USD" : "Bs")}";
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

        var request = new CreateSaleRequest(
            items, method, received, Discount, customerId, null, null,
            string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim(),
            invoice,
            PaysUsd ? Currency.Usd : Currency.Ves,
            null, null);

        _payButton.IsEnabled = false;
        _payButton.Content = "Procesando...";

        try
        {
            var sale = await App.Api.CreateSaleAsync(request);
            var ticketSettings = TicketSettings.From(_settings);

            if (ticketSettings.AutoPrint && !string.IsNullOrWhiteSpace(ticketSettings.PrinterName))
            {
                try { TicketPrinter.PrintSale(sale, ticketSettings, silent: true); }
                catch { /* La venta ya quedó registrada; se puede reimprimir. */ }
            }

            var dialog = new SaleReceiptDialog(sale, ticketSettings) { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();

            _cart.Clear();
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

public sealed class PosLine
{
    public PosLine(ProductDto product) => Product = product;
    public ProductDto Product { get; }
    public decimal Quantity { get; set; } = 1;
    public decimal Total => Math.Round(Product.Price * Quantity, 2);
}

public sealed class SaleReceiptDialog : FormDialog
{
    public SaleReceiptDialog(SaleDto sale, TicketSettings? ticketSettings = null)
        : base($"Venta {sale.Number}", $"{Labels.InvoiceName(sale.InvoiceType)} · {Labels.PaymentName(sale.Method)}")
    {
        AcceptButton.Content = "Aceptar";
        HideCancel();

        AddRow("Fecha", sale.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        if (!string.IsNullOrWhiteSpace(sale.ControlNumber))
            AddRow("N° de control", sale.ControlNumber!);
        if (!string.IsNullOrWhiteSpace(sale.CustomerName))
            AddRow("Cliente", sale.CustomerName!);

        AddSeparator();

        foreach (var item in sale.Items)
            AddRow($"{item.Quantity:0.##} × {item.Description}", $"$ {item.Total:N2}");

        AddSeparator();
        AddRow("Subtotal", $"$ {sale.Subtotal:N2}");
        if (sale.Discount > 0) AddRow("Descuento", $"- $ {sale.Discount:N2}");
        AddRow("IVA", $"$ {sale.Tax:N2}");
        if (sale.IgtfAmount > 0) AddRow("IGTF", $"$ {sale.IgtfAmount:N2}");
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

        var print = Kit.Button("Imprimir ticket", "BlueButton", () => TryPrint());
        var printAndAccept = Kit.Button("Imprimir y aceptar", "PrimaryButton", () =>
        {
            if (TryPrint()) DialogResult = true;
        });

        var printActions = Kit.HStack(8, print, printAndAccept);
        printActions.HorizontalAlignment = HorizontalAlignment.Left;
        printActions.Margin = new Thickness(0, 12, 0, 0);
        Body.Children.Add(printActions);

        AddNote(sale.Status == SaleStatus.Cancelled
            ? "Esta venta está anulada."
            : "Venta registrada en la caja del turno. Use «Aceptar» para cerrar el mensaje.",
            "SuccessBrush");
    }
}

