using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CyberManager.Desktop.Mvvm;
using CyberManager.Desktop.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Client;

public partial class OrderWindow : Window
{
    private readonly ObservableRange<OrderProduct> _products = new();
    private readonly ObservableCollection<CartLine> _cart = new();
    private List<OrderProduct> _all = new();
    private readonly decimal _rate;
    private bool _submitting;

    public OrderWindow()
    {
        InitializeComponent();

        ProductsList.ItemsSource = _products;
        CartList.ItemsSource = _cart;

        _cart.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (CartLine line in e.NewItems) line.PropertyChanged += Line_Changed;
            if (e.OldItems is not null)
                foreach (CartLine line in e.OldItems) line.PropertyChanged -= Line_Changed;
            RefreshTotals();
        };

        var state = App.State;
        _rate = state?.ExchangeRate ?? 0;
        StationText.Text = state?.Name ?? $"PC-{App.Config.StationId:D2}";

        var session = state?.Session;
        SessionText.Text = session is null
            ? L.T("Sesión activa")
            : L.IsEnglish
                ? $"Session #{session.Id} · {session.CustomerName ?? L.T("Cliente")}"
                : $"Sesión #{session.Id} · {session.CustomerName ?? "Cliente"}";

        Loaded += async (_, _) =>
        {
            Loc.Apply(this);
            await LoadProductsAsync();
        };
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            if (App.Api is null) return;

            var products = await App.Api.ProductsAsync();
            _all = products.Select(p => new OrderProduct(p, _rate)).ToList();

            var options = new List<CategoryOption> { new(null, L.T("Todas las categorías")) };
            options.AddRange(_all
                .Where(p => p.Product.CategoryId is not null)
                .GroupBy(p => new { p.Product.CategoryId, p.Category })
                .Select(g => new CategoryOption(g.Key.CategoryId, g.Key.Category))
                .OrderBy(o => o.Name));

            CategoryBox.ItemsSource = options;
            CategoryBox.DisplayMemberPath = nameof(CategoryOption.Name);
            CategoryBox.SelectedIndex = 0;

            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, L.T("No se pudo cargar el catálogo"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void ApplyFilter()
    {
        var term = SearchBox.Text?.Trim() ?? string.Empty;
        var categoryId = (CategoryBox.SelectedItem as CategoryOption)?.Id;

        var filtered = _all.Where(p =>
            (categoryId is null || p.Product.CategoryId == categoryId) &&
            (term.Length == 0 ||
             p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
             p.Product.Sku.Contains(term, StringComparison.OrdinalIgnoreCase)));

        _products.ReplaceAll(filtered);
    }

    private void Category_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void Product_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: OrderProduct product } || !product.CanOrder) return;

        var line = _cart.FirstOrDefault(l => l.Item.Product.Id == product.Product.Id);
        if (line is null) _cart.Add(new CartLine(product));
        else line.Quantity += 1;
    }

    private void Plus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CartLine line }) line.Quantity += 1;
    }

    private void Minus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CartLine line }) return;
        if (line.Quantity <= 1) _cart.Remove(line);
        else line.Quantity -= 1;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CartLine line }) _cart.Remove(line);
    }

    private void Quantity_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CartLine line })
        {
            line.Quantity = Math.Max(1, line.Quantity);
            line.NotifyQuantity();
        }
        RefreshTotals();
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => _cart.Clear();

    private void Line_Changed(object? sender, PropertyChangedEventArgs e) => RefreshTotals();

    private void RefreshTotals()
    {
        var total = _cart.Sum(l => l.TotalUsd);
        TotalUsdText.Text = $"$ {total:N2}";
        TotalBsText.Text = $"Bs {total * _rate:N2}";
        EmptyText.Visibility = _cart.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SubmitButton.IsEnabled = _cart.Count > 0 && !_submitting;
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (_submitting || _cart.Count == 0 || App.Api is null) return;

        _submitting = true;
        SubmitButton.IsEnabled = false;
        SubmitButton.Content = "Enviando...";

        try
        {
            var items = _cart.Select(l => new StationOrderItem(l.Item.Product.Id, l.Quantity)).ToList();
            var sale = await App.Api.OrderAsync(new StationOrderRequest(items));

            MessageBox.Show(this,
                $"Pedido enviado correctamente.\n\nTotal: $ {sale.Total:N2}  (Bs {sale.TotalVes:N2})\n" +
                "El consumo se cargará a su cuenta al cerrar la sesión.",
                "Pedido enviado", MessageBoxButton.OK, MessageBoxImage.Information);

            _cart.Clear();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "No se pudo enviar el pedido",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _submitting = false;
            SubmitButton.Content = "Enviar pedido a la estación";
            RefreshTotals();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public sealed record CategoryOption(int? Id, string Name);

public sealed class OrderProduct
{
    public ProductDto Product { get; }
    public decimal Rate { get; }

    public OrderProduct(ProductDto product, decimal rate)
    {
        Product = product;
        Rate = rate;
    }

    public string Name => Product.Name;
    public string Category => Product.CategoryName ?? "General";
    public string PriceUsd => $"$ {Product.Price:N2}";
    public string PriceBs => $"Bs {Product.Price * Rate:N2}";
    public bool CanOrder => Product.IsService || Product.Stock > 0;
    public string StockText => Product.IsService
        ? "Servicio"
        : Product.Stock <= 0 ? "Agotado" : $"{Product.Stock:N0} disp.";
}

public sealed class CartLine : ObservableObject
{
    private decimal _quantity;

    public CartLine(OrderProduct item, decimal quantity = 1)
    {
        Item = item;
        _quantity = Math.Max(1, quantity);
    }

    public OrderProduct Item { get; }
    public string Name => Item.Name;
    public string UnitPriceText => $"{Item.PriceUsd} · {Item.PriceBs}";

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            var clamped = Math.Max(1, decimal.Round(value, 2));
            if (!SetProperty(ref _quantity, clamped)) return;
            OnPropertyChanged(nameof(QuantityText));
            OnPropertyChanged(nameof(TotalUsd));
            OnPropertyChanged(nameof(TotalUsdText));
        }
    }

    public string QuantityText
    {
        get => _quantity.ToString("0.##", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var parsed))
                Quantity = parsed;
            else
                OnPropertyChanged();
        }
    }

    public decimal TotalUsd => Math.Round(Item.Product.Price * _quantity, 2);
    public string TotalUsdText => $"$ {TotalUsd:N2}";

    public void NotifyQuantity() => OnPropertyChanged(nameof(QuantityText));
}
