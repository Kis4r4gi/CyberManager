using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CyberManager.Desktop.Api;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

public sealed class CashView : UserControl, IRefreshable
{
    private readonly TextBlock _statusText = Kit.Muted("Consultando caja...");
    private readonly StackPanel _sessionPanel = new();
    private readonly DataGrid _movementsGrid;
    private readonly DataGrid _historyGrid;
    private readonly UniformGrid _metrics = new() { Columns = 6, Margin = new Thickness(0, 0, 0, 16) };

    private readonly TextBlock _openingValue;
    private readonly TextBlock _cashSalesValue;
    private readonly TextBlock _usdSalesValue;
    private readonly TextBlock _cardSalesValue;
    private readonly TextBlock _outflowsValue;
    private readonly TextBlock _expectedValue;

    private CashSessionDto? _cash;
    private decimal _rate;

    public CashView()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var statusPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        _statusText.HorizontalAlignment = HorizontalAlignment.Right;
        statusPanel.Children.Add(_statusText);
        statusPanel.Children.Add(Kit.Muted("Todos los importes se guardan en USD."));

        root.Children.Add(Kit.PageHeader("Caja del turno",
            "Apertura, movimientos, arqueo y cierre de caja en bolívares y dólares.",
            Kit.Button("Actualizar", "GhostButton", () => _ = RefreshAsync()),
            statusPanel));

        _metrics.Children.Add(Metric("Fondo inicial", "Accent2Brush", out _openingValue));
        _metrics.Children.Add(Metric("Ventas efectivo Bs", "SuccessBrush", out _cashSalesValue));
        _metrics.Children.Add(Metric("Ventas efectivo USD", "PurpleBrush", out _usdSalesValue));
        _metrics.Children.Add(Metric("Tarjeta / transferencia", "AccentBrush", out _cardSalesValue));
        _metrics.Children.Add(Metric("Gastos y retiros", "WarningBrush", out _outflowsValue));
        _metrics.Children.Add(Metric("Esperado en caja", "DangerBrush", out _expectedValue));
        Grid.SetRow(_metrics, 1);
        root.Children.Add(_metrics);

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(372) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sessionCard = Kit.Card(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _sessionPanel
        }, 16);
        sessionCard.Margin = new Thickness(0, 0, 14, 0);
        content.Children.Add(sessionCard);

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });

        _movementsGrid = Kit.Table(
            Kit.Col("Hora", nameof(MovementRow.Time), 80),
            Kit.Col("Tipo", nameof(MovementRow.Type), 110),
            Kit.Col("Método", nameof(MovementRow.Method), 110),
            Kit.Col("Descripción", nameof(MovementRow.Description)),
            Kit.Col("USD", nameof(MovementRow.AmountUsd), 100, "{0:N2}"),
            Kit.Col("Bs", nameof(MovementRow.AmountVes), 110, "{0:N2}"));

        _historyGrid = Kit.Table(
            Kit.Col("Apertura", nameof(CashHistoryRow.Opened), 140),
            Kit.Col("Cierre", nameof(CashHistoryRow.Closed), 140),
            Kit.Col("Usuario", nameof(CashHistoryRow.User), 140),
            Kit.Col("Fondo", nameof(CashHistoryRow.Opening), 90, "{0:N2}"),
            Kit.Col("Ventas", nameof(CashHistoryRow.Sales), 90, "{0:N2}"),
            Kit.Col("Esperado", nameof(CashHistoryRow.Expected), 95, "{0:N2}"),
            Kit.Col("Contado", nameof(CashHistoryRow.Closing), 95, "{0:N2}"),
            Kit.Col("Diferencia", nameof(CashHistoryRow.Difference), 95, "{0:N2}"));

        var movementsCard = Kit.Card(new Grid
        {
            Children =
            {
                Labeled("Movimientos del turno", _movementsGrid)
            }
        }, 14);
        movementsCard.Margin = new Thickness(0, 0, 0, 12);
        right.Children.Add(movementsCard);

        var historyCard = Kit.Card(Labeled("Historial de cajas (30 días)", _historyGrid), 14);
        Grid.SetRow(historyCard, 1);
        right.Children.Add(historyCard);

        Grid.SetColumn(right, 1);
        content.Children.Add(right);
        Grid.SetRow(content, 2);
        root.Children.Add(content);

        Content = root;
        Loaded += async (_, _) => await RefreshAsync();
    }

    private static Border Metric(string label, string accent, out TextBlock valueText)
    {
        var card = Kit.StatCard(label, "—", null, accent, out var value);
        card.Margin = new Thickness(0, 0, 10, 0);
        valueText = value;
        return card;
    }

    private static Grid Labeled(string title, FrameworkElement content)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(Kit.Section(title));
        Grid.SetRow(content, 1);
        grid.Children.Add(content);
        return grid;
    }

    public async Task RefreshAsync()
    {
        _rate = (await App.Api.TodayRateAsync()).Rate;

        CashSessionDto? cash = null;
        try
        {
            cash = await App.Api.CashCurrentAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            cash = null;
        }

        _cash = cash;

        List<CashReportRow> history;
        try
        {
            history = await App.Api.CashHistoryAsync(DateTime.Today.AddDays(-30), DateTime.Today);
        }
        catch
        {
            history = new List<CashReportRow>();
        }

        Render(cash, history);
    }

    private void Render(CashSessionDto? cash, List<CashReportRow> history)
    {
        _statusText.Text = cash is null
            ? "No hay una caja abierta"
            : $"Caja abierta desde {cash.OpenedUtc.ToLocalTime():HH:mm} · 1 USD = {_rate:N2} Bs";
        _statusText.Foreground = cash is null ? Kit.Brush("WarningBrush") : Kit.Brush("SuccessBrush");

        if (cash is null)
        {
            _openingValue.Text = "—";
            _cashSalesValue.Text = "—";
            _usdSalesValue.Text = "—";
            _cardSalesValue.Text = "—";
            _outflowsValue.Text = "—";
            _expectedValue.Text = "—";

            _movementsGrid.ItemsSource = Array.Empty<MovementRow>();
        }
        else
        {
            _openingValue.Text = $"$ {cash.OpeningAmount:N2}";
            _cashSalesValue.Text = $"$ {cash.SalesCash:N2}";
            _usdSalesValue.Text = $"$ {cash.SalesCashUsd:N2}";
            _cardSalesValue.Text = $"$ {cash.SalesCard + cash.SalesTransfer:N2}";
            _outflowsValue.Text = $"$ {cash.Expenses + cash.Withdrawals + cash.Refunds:N2}";
            _expectedValue.Text = $"$ {cash.ExpectedAmount:N2}";
            _openingValue.ToolTip = $"Tasa de apertura: {cash.OpeningExchangeRate:N2} Bs/USD";
            _expectedValue.ToolTip = cash.OpeningExchangeRate > 0
                ? $"En bolívares: Bs {cash.ExpectedAmount * cash.OpeningExchangeRate:N2}"
                : null;

            _movementsGrid.ItemsSource = cash.Movements
                .OrderByDescending(m => m.CreatedUtc)
                .Select(m => new MovementRow(m))
                .ToList();
        }

        _historyGrid.ItemsSource = history.Select(h => new CashHistoryRow(h)).ToList();

        RenderSessionPanel(cash);
    }

    private void RenderSessionPanel(CashSessionDto? cash)
    {
        _sessionPanel.Children.Clear();

        if (cash is null)
        {
            _sessionPanel.Children.Add(Kit.Section("Abrir caja"));
            _sessionPanel.Children.Add(new TextBlock
            {
                Text = "Registre el fondo inicial para comenzar el turno. La tasa del día se toma automáticamente.",
                Style = Kit.Style("Muted"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });

            var opening = Kit.Input("0,00");
            _sessionPanel.Children.Add(Kit.Field("Fondo inicial en caja (USD)", opening));
            _sessionPanel.Children.Add(Kit.Field("Tasa del día (Bs por USD)",
                new TextBox { Text = _rate > 0 ? _rate.ToString("N2") : string.Empty, IsReadOnly = true }));
            var notes = Kit.Multiline(null, 60);
            _sessionPanel.Children.Add(Kit.Field("Notas de apertura", notes));

            if (_rate <= 0)
            {
                _sessionPanel.Children.Add(new TextBlock
                {
                    Text = "Debe registrar la tasa de cambio del día en Configuración antes de abrir la caja.",
                    Foreground = Kit.Brush("DangerBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 8)
                });
            }

            var open = Kit.Button("Abrir caja", "SuccessButton", async () =>
            {
                var amount = ParseMoney(opening.Text);
                var request = new CashOpenRequest(amount, _rate > 0 ? _rate : null,
                    string.IsNullOrWhiteSpace(notes.Text) ? null : notes.Text.Trim());

                await Ui.Run(async () =>
                {
                    await App.Api.CashOpenAsync(request);
                    await RefreshAsync();
                }, "No se pudo abrir la caja");
            });
            open.Padding = new Thickness(14, 12, 14, 12);
            open.Margin = new Thickness(0, 8, 0, 0);
            _sessionPanel.Children.Add(open);

            _sessionPanel.Children.Add(new Separator { Margin = new Thickness(0, 16, 0, 12) });
            _sessionPanel.Children.Add(new TextBlock
            {
                Text = "Consejo: si el turno anterior quedó abierto por error, ciérrelo desde el historial antes de abrir uno nuevo.",
                Style = Kit.Style("Muted"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11
            });
            return;
        }

        _sessionPanel.Children.Add(Kit.Section("Turno actual"));
        _sessionPanel.Children.Add(new TextBlock
        {
            Text = $"Abierto por {cash.OpenedByName ?? "usuario"} · {cash.OpenedUtc.ToLocalTime():dd/MM/yyyy HH:mm}",
            Style = Kit.Style("Muted"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        AddSummary("Fondo inicial", cash.OpeningAmount);
        AddSummary("Ventas efectivo Bs", cash.SalesCash);
        AddSummary("Ventas efectivo USD", cash.SalesCashUsd);
        AddSummary("Tarjeta", cash.SalesCard);
        AddSummary("Transferencias", cash.SalesTransfer);
        AddSummary("Depósitos", cash.Deposits);
        AddSummary("Retiros", -cash.Withdrawals);
        AddSummary("Gastos", -cash.Expenses);
        AddSummary("Devoluciones", -cash.Refunds);

        _sessionPanel.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });

        var expectedRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        expectedRow.Children.Add(new TextBlock { Text = "Esperado en caja", FontWeight = FontWeights.SemiBold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        var expected = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
        expected.Children.Add(new TextBlock
        {
            Text = $"$ {cash.ExpectedAmount:N2}",
            FontWeight = FontWeights.Bold,
            FontSize = 18,
            Foreground = Kit.Brush("AccentBrush"),
            HorizontalAlignment = HorizontalAlignment.Right
        });
        if (cash.OpeningExchangeRate > 0)
        {
            expected.Children.Add(new TextBlock
            {
                Text = $"Bs {cash.ExpectedAmount * cash.OpeningExchangeRate:N2}",
                Style = Kit.Style("Muted"),
                HorizontalAlignment = HorizontalAlignment.Right
            });
        }
        expectedRow.Children.Add(expected);
        _sessionPanel.Children.Add(expectedRow);

        var buttons = new UniformGrid { Columns = 2, Margin = new Thickness(0, 14, 0, 0) };

        var movement = new Button
        {
            Content = "Movimiento",
            Style = Kit.Style("BlueButton"),
            Margin = new Thickness(0, 0, 4, 0),
            Padding = new Thickness(10, 10, 10, 10)
        };
        movement.Click += (_, _) => OpenMovementDialog();
        buttons.Children.Add(movement);

        var close = new Button
        {
            Content = "Cerrar caja",
            Style = Kit.Style("WarningButton"),
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(10, 10, 10, 10)
        };
        close.Click += (_, _) => OpenCloseDialog();
        buttons.Children.Add(close);

        _sessionPanel.Children.Add(buttons);

        _sessionPanel.Children.Add(new TextBlock
        {
            Text = "El cierre compara el efectivo contado con el esperado y guarda la diferencia.",
            Style = Kit.Style("Muted"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Margin = new Thickness(0, 12, 0, 0)
        });
    }

    private void AddSummary(string label, decimal amount)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(new TextBlock { Text = label, Style = Kit.Style("Muted"), VerticalAlignment = VerticalAlignment.Center });
        var value = new TextBlock
        {
            Text = $"$ {amount:N2}",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold
        };
        if (amount < 0) value.Foreground = Kit.Brush("DangerBrush");
        row.Children.Add(value);
        _sessionPanel.Children.Add(row);
    }

    private void OpenMovementDialog()
    {
        if (_cash is null) return;

        var dialog = new CashMovementDialog(_cash) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.CashMovementAsync(dialog.Result);
            await RefreshAsync();
        }, "No se pudo registrar el movimiento");
    }

    private void OpenCloseDialog()
    {
        if (_cash is null) return;

        var dialog = new CashCloseDialog(_cash) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            var closed = await App.Api.CashCloseAsync(dialog.Result);
            var difference = closed.Difference;
            Ui.Info(
                $"Caja cerrada.\n\nEsperado: $ {closed.ExpectedAmount:N2}\n" +
                $"Contado: $ {closed.ClosingAmount ?? 0:N2}\n" +
                $"Diferencia: $ {difference:N2}",
                Math.Abs(difference) < 0.01m ? "Caja cuadrada" : "Diferencia registrada");
            await RefreshAsync();
        }, "No se pudo cerrar la caja");
    }

    private static decimal ParseMoney(string? text)
        => decimal.TryParse(text, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.CurrentCulture, out var value) ? value : 0m;
}

public sealed class CashMovementDialog : FormDialog
{
    private readonly ComboBox _typeBox;
    private readonly ComboBox _methodBox;
    private readonly TextBox _amountBox;
    private readonly TextBox _descriptionBox;

    public CashMovementRequest? Result { get; private set; }

    public CashMovementDialog(CashSessionDto cash)
        : base("Movimiento de caja", "Registre depósitos, retiros, gastos o devoluciones en efectivo.")
    {
        AddRow("Esperado actual", $"$ {cash.ExpectedAmount:N2}");

        _typeBox = AddCombo("Tipo de movimiento", new[]
        {
            new Option<CashMovementType>(CashMovementType.Deposit, "Depósito de efectivo"),
            new Option<CashMovementType>(CashMovementType.Withdrawal, "Retiro de efectivo"),
            new Option<CashMovementType>(CashMovementType.Expense, "Gasto / pago a proveedor"),
            new Option<CashMovementType>(CashMovementType.Refund, "Devolución al cliente")
        });

        _methodBox = AddCombo("Forma de pago", new[]
        {
            new Option<PaymentMethod>(PaymentMethod.Cash, "Efectivo en bolívares"),
            new Option<PaymentMethod>(PaymentMethod.CashUsd, "Efectivo en dólares")
        });

        _amountBox = Add(new TextBox { Text = "0,00" }, "Monto en USD");
        _descriptionBox = Add(new TextBox(), "Descripción");
        AddNote("El monto se convierte a bolívares con la tasa del día y queda registrado en el historial.");
    }

    protected override void OnAccept()
    {
        var type = ((Option<CashMovementType>)_typeBox.SelectedItem!).Value;
        var method = ((Option<PaymentMethod>)_methodBox.SelectedItem!).Value;
        var amount = ParseDecimal(_amountBox, "Monto", 0.01m);
        var description = string.IsNullOrWhiteSpace(_descriptionBox.Text)
            ? Labels.CashName(type)
            : _descriptionBox.Text.Trim();

        Result = new CashMovementRequest(type, method, amount, description);
        base.OnAccept();
    }
}

public sealed class CashCloseDialog : FormDialog
{
    private readonly TextBox _closingBox;
    private readonly TextBox _notesBox;

    public CashCloseRequest? Result { get; private set; }

    public CashCloseDialog(CashSessionDto cash)
        : base("Cerrar caja", "Cuente el efectivo y registre el monto final del turno.")
    {
        AddRow("Fondo inicial", $"$ {cash.OpeningAmount:N2}");
        AddRow("Ventas en efectivo Bs", $"$ {cash.SalesCash:N2}");
        AddRow("Ventas en efectivo USD", $"$ {cash.SalesCashUsd:N2}");
        AddRow("Depósitos", $"$ {cash.Deposits:N2}");
        AddRow("Gastos y retiros", $"- $ {cash.Expenses + cash.Withdrawals:N2}");
        AddSeparator();
        AddRow("Esperado en caja", $"$ {cash.ExpectedAmount:N2}", highlight: true);
        AddSeparator();

        _closingBox = Add(new TextBox { Text = cash.ExpectedAmount.ToString("N2") }, "Efectivo contado en USD");
        _notesBox = Add(new TextBox(), "Notas del cierre");

        AddNote("La diferencia se calcula automáticamente y quedará en el historial del turno.");
    }

    protected override void OnAccept()
    {
        var closing = ParseDecimal(_closingBox, "Efectivo contado");
        Result = new CashCloseRequest(closing,
            string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim());
        base.OnAccept();
    }
}

public sealed class MovementRow
{
    public MovementRow(CashMovementDto movement)
    {
        Time = movement.CreatedUtc.ToLocalTime().ToString("HH:mm");
        Type = Labels.CashName(movement.Type);
        Method = Labels.PaymentName(movement.Method);
        Description = movement.Description;
        AmountUsd = movement.Amount;
        AmountVes = movement.AmountVes;
    }

    public string Time { get; }
    public string Type { get; }
    public string Method { get; }
    public string Description { get; }
    public decimal AmountUsd { get; }
    public decimal AmountVes { get; }
}

public sealed class CashHistoryRow
{
    public CashHistoryRow(CashReportRow row)
    {
        Opened = row.OpenedUtc.ToLocalTime().ToString("dd/MM HH:mm");
        Closed = row.ClosedUtc?.ToLocalTime().ToString("dd/MM HH:mm") ?? "Abierta";
        User = row.UserName ?? "—";
        Opening = row.OpeningAmount;
        Sales = row.Sales;
        Expected = row.Expected;
        Closing = row.Closing ?? 0m;
        Difference = row.Difference;
        DifferenceBrush = row.Difference < 0 ? "#EF4444" : row.Difference > 0 ? "#F59E0B" : "#22C55E";
    }

    public string Opened { get; }
    public string Closed { get; }
    public string User { get; }
    public decimal Opening { get; }
    public decimal Sales { get; }
    public decimal Expected { get; }
    public decimal Closing { get; }
    public decimal Difference { get; }
    public string DifferenceBrush { get; }
}
