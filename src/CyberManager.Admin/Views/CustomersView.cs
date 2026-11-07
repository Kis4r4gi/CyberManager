using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

/// <summary>Clientes, saldos a favor, membresías e historial de movimientos.</summary>
public sealed class CustomersView : UserControl, IRefreshable
{
    private readonly DataGrid _grid;
    private readonly TextBox _searchBox = new();
    private readonly TextBlock _countValue;
    private readonly TextBlock _membersValue;
    private readonly TextBlock _balanceValue;
    private readonly TextBlock _creditValue;
    private readonly TextBlock _summaryText = Kit.Muted("Cargando clientes...");

    private List<CustomerDto> _customers = new();

    public CustomersView()
    {
        _grid = Kit.Table(
            Kit.Col("Cliente", nameof(CustomerRow.Name), 210),
            Kit.Col("Documento / RIF", nameof(CustomerRow.Document), 145),
            Kit.Col("Teléfono", nameof(CustomerRow.Phone), 130),
            Kit.Col("Correo", nameof(CustomerRow.Email), 190),
            Kit.Col("Saldo USD", nameof(CustomerRow.Balance), 105, "{0:N2}"),
            Kit.Col("Límite crédito", nameof(CustomerRow.CreditLimit), 110, "{0:N2}"),
            Kit.Col("Miembro", nameof(CustomerRow.Member), 90),
            Kit.Col("Estado", nameof(CustomerRow.Status), 90),
            Kit.Col("Registrado", nameof(CustomerRow.Created), 110));

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(Kit.PageHeader("Clientes y saldos",
            "Registre clientes frecuentes, recargue saldo y consulte el historial de consumo.",
            Kit.Button("Nuevo cliente", "PrimaryButton", NewCustomer),
            Kit.Button("Actualizar", "GhostButton", () => _ = Ui.Run(RefreshAsync, "No se pudieron cargar los clientes"))));

        var metrics = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 16) };
        metrics.Children.Add(Metric("Clientes activos", "AccentBrush", out _countValue));
        metrics.Children.Add(Metric("Miembros", "PurpleBrush", out _membersValue));
        metrics.Children.Add(Metric("Saldo a favor", "SuccessBrush", out _balanceValue));
        metrics.Children.Add(Metric("Crédito otorgado", "WarningBrush", out _creditValue));
        Grid.SetRow(metrics, 1);
        root.Children.Add(metrics);

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _searchBox.ToolTip = "Buscar por nombre, documento, teléfono o correo";
        _searchBox.TextChanged += (_, _) => Render();
        toolbar.Children.Add(_searchBox);

        var actions = Kit.HStack(8,
            Kit.Button("Editar", "GhostButton", EditCustomer),
            Kit.Button("Recargar saldo", "SuccessButton", RechargeCustomer),
            Kit.Button("Movimientos", "BlueButton", ShowHistory),
            Kit.Button("Activar / desactivar", "GhostButton", ToggleActive));
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(actions, 1);
        toolbar.Children.Add(actions);
        content.Children.Add(toolbar);

        Grid.SetRow(_grid, 1);
        content.Children.Add(_grid);

        Grid.SetRow(content, 2);
        root.Children.Add(content);

        var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        footer.Children.Add(_summaryText);
        var hint = Kit.Muted("El saldo se descuenta cuando el cliente paga con «Saldo del cliente».");
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        footer.Children.Add(hint);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        Content = root;

        _grid.MouseDoubleClick += (_, _) => EditCustomer();

        Loaded += async (_, _) => await Ui.Run(RefreshAsync, "No se pudieron cargar los clientes");
    }

    private static Border Metric(string label, string accent, out TextBlock valueText)
    {
        var card = Kit.StatCard(label, "—", null, accent, out var value);
        card.Margin = new Thickness(0, 0, 10, 0);
        valueText = value;
        return card;
    }

    private CustomerDto? Selected => (_grid.SelectedItem as CustomerRow)?.Source;

    public async Task RefreshAsync()
    {
        _customers = await App.Api.CustomersAsync();
        Render();
    }

    private void Render()
    {
        var term = _searchBox.Text.Trim();

        var filtered = _customers
            .Where(c => term.Length == 0
                        || c.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || (c.DocumentId?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (c.Phone?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (c.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(c => c.Name)
            .ToList();

        _grid.ItemsSource = filtered.Select(c => new CustomerRow(c)).ToList();

        var memberCount = _customers.Count(c => c.IsMember);
        var balance = _customers.Sum(c => c.Balance);
        var credit = _customers.Sum(c => c.CreditLimit);

        _countValue.Text = _customers.Count.ToString();
        _membersValue.Text = memberCount.ToString();
        _balanceValue.Text = Ui.Money(balance);
        _creditValue.Text = Ui.Money(credit);

        _countValue.ToolTip = $"{filtered.Count} visibles con el filtro actual";
        _balanceValue.ToolTip = "Suma de saldos a favor de todos los clientes activos";
        _creditValue.ToolTip = "Límite de crédito acumulado";

        _summaryText.Text = $"{filtered.Count} clientes visibles · {memberCount} miembros · " +
                            $"saldo total {Ui.Money(balance)}";
    }

    private void NewCustomer()
    {
        var dialog = new CustomerDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveCustomerAsync(null, dialog.Result);
            await RefreshAsync();
        }, "No se pudo crear el cliente");
    }

    private void EditCustomer()
    {
        if (Selected is not { } customer)
        {
            Ui.Info("Seleccione un cliente de la lista.", "Clientes");
            return;
        }

        var dialog = new CustomerDialog(customer) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            await App.Api.SaveCustomerAsync(customer.Id, dialog.Result);
            await RefreshAsync();
        }, "No se pudo guardar el cliente");
    }

    private void ToggleActive()
    {
        if (Selected is not { } customer)
        {
            Ui.Info("Seleccione un cliente de la lista.", "Clientes");
            return;
        }

        var enable = !customer.Active;
        if (!Ui.Confirm(enable
                ? $"¿Reactivar a {customer.Name}?"
                : $"¿Desactivar a {customer.Name}? No aparecerá en la caja hasta reactivarlo.",
                "Cliente"))
            return;

        _ = Ui.Run(async () =>
        {
            var request = new SaveCustomerRequest(customer.Name, customer.Phone, customer.Email,
                customer.DocumentId, customer.Address, customer.FiscalData, customer.CreditLimit,
                customer.IsMember, enable, customer.Notes);
            await App.Api.SaveCustomerAsync(customer.Id, request);
            await RefreshAsync();
        }, "No se pudo actualizar el cliente");
    }

    private void RechargeCustomer()
    {
        if (Selected is not { } customer)
        {
            Ui.Info("Seleccione un cliente de la lista.", "Clientes");
            return;
        }

        var dialog = new CustomerRechargeDialog(customer) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        _ = Ui.Run(async () =>
        {
            var updated = await App.Api.RechargeAsync(customer.Id, dialog.Result);
            Ui.Info($"Recarga registrada. Nuevo saldo de {updated.Name}: {Ui.Money(updated.Balance)}.",
                "Saldo actualizado");
            await RefreshAsync();
        }, "No se pudo recargar el saldo");
    }

    private void ShowHistory()
    {
        if (Selected is not { } customer)
        {
            Ui.Info("Seleccione un cliente de la lista.", "Clientes");
            return;
        }

        var owner = Window.GetWindow(this);

        _ = Ui.Run(async () =>
        {
            var transactions = await App.Api.CustomerTransactionsAsync(customer.Id);
            var dialog = new CustomerHistoryDialog(customer, transactions) { Owner = owner };
            dialog.ShowDialog();
        }, "No se pudo cargar el historial del cliente");
    }
}

public sealed class CustomerRow
{
    public CustomerRow(CustomerDto customer)
    {
        Source = customer;
        Name = customer.Name;
        Document = string.IsNullOrWhiteSpace(customer.DocumentId) ? "—" : customer.DocumentId!;
        Phone = string.IsNullOrWhiteSpace(customer.Phone) ? "—" : customer.Phone!;
        Email = string.IsNullOrWhiteSpace(customer.Email) ? "—" : customer.Email!;
        Balance = customer.Balance;
        CreditLimit = customer.CreditLimit;
        Member = customer.IsMember ? "Sí" : "No";
        Status = customer.Active ? "Activo" : "Inactivo";
        Created = customer.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy");
    }

    public CustomerDto Source { get; }
    public string Name { get; }
    public string Document { get; }
    public string Phone { get; }
    public string Email { get; }
    public decimal Balance { get; }
    public decimal CreditLimit { get; }
    public string Member { get; }
    public string Status { get; }
    public string Created { get; }
}

public sealed class CustomerDialog : FormDialog
{
    private readonly TextBox _name;
    private readonly TextBox _document;
    private readonly TextBox _phone;
    private readonly TextBox _email;
    private readonly TextBox _address;
    private readonly TextBox _fiscal;
    private readonly TextBox _credit;
    private readonly TextBox _notes;
    private readonly CheckBox _member;
    private readonly CheckBox _active;

    public SaveCustomerRequest? Result { get; private set; }

    public CustomerDialog(CustomerDto? customer = null)
        : base(customer is null ? "Nuevo cliente" : $"Editar {customer.Name}",
            "El documento/RIF se usa en la factura. El saldo a favor se descuenta al cobrar.")
    {
        _name = Add(new TextBox { Text = customer?.Name ?? string.Empty }, "Nombre o razón social");
        _document = Add(new TextBox { Text = customer?.DocumentId ?? string.Empty }, "Cédula o RIF");
        _phone = Add(new TextBox { Text = customer?.Phone ?? string.Empty }, "Teléfono");
        _email = Add(new TextBox { Text = customer?.Email ?? string.Empty }, "Correo electrónico");
        _address = Add(new TextBox { Text = customer?.Address ?? string.Empty }, "Dirección fiscal");
        _fiscal = Add(new TextBox { Text = customer?.FiscalData ?? string.Empty }, "Datos fiscales adicionales");
        _credit = Add(new TextBox { Text = (customer?.CreditLimit ?? 0m).ToString("N2", CultureInfo.CurrentCulture) },
            "Límite de crédito (USD)");
        _notes = Add(new TextBox { Text = customer?.Notes ?? string.Empty }, "Notas");
        _member = AddCheck("Es miembro / cliente frecuente", customer?.IsMember ?? false);
        _active = AddCheck("Cliente activo", customer?.Active ?? true);
    }

    protected override void OnAccept()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
            throw new InvalidOperationException("Indique el nombre del cliente.");

        Result = new SaveCustomerRequest(
            _name.Text.Trim(),
            NullIfEmpty(_document.Text),
            NullIfEmpty(_phone.Text),
            NullIfEmpty(_email.Text),
            NullIfEmpty(_address.Text),
            NullIfEmpty(_fiscal.Text),
            ParseDecimal(_credit, "Límite de crédito"),
            _member.IsChecked == true,
            _active.IsChecked == true,
            NullIfEmpty(_notes.Text));

        base.OnAccept();
    }

    private static string? NullIfEmpty(string text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

public sealed class CustomerRechargeDialog : FormDialog
{
    private readonly TextBox _amount;
    private readonly ComboBox _methodBox;
    private readonly TextBox _description;

    public RechargeRequest? Result { get; private set; }

    public CustomerRechargeDialog(CustomerDto customer)
        : base($"Recargar saldo · {customer.Name}",
            "La recarga aumenta el saldo a favor del cliente y se registra en la caja abierta.")
    {
        AddRow("Saldo actual", Ui.Money(customer.Balance));
        AddRow("Límite de crédito", Ui.Money(customer.CreditLimit));
        AddSeparator();

        _amount = Add(new TextBox { Text = "0,00" }, "Monto a recargar (USD)");
        _methodBox = AddCombo("Forma de pago recibida", new[]
        {
            new Option<PaymentMethod>(PaymentMethod.Cash, "Efectivo en bolívares"),
            new Option<PaymentMethod>(PaymentMethod.CashUsd, "Efectivo en dólares"),
            new Option<PaymentMethod>(PaymentMethod.Card, "Punto de venta / tarjeta"),
            new Option<PaymentMethod>(PaymentMethod.Transfer, "Transferencia"),
            new Option<PaymentMethod>(PaymentMethod.PagoMovil, "Pago Móvil")
        });
        _description = Add(new TextBox(), "Descripción (opcional)");

        AddNote("Si paga en efectivo, tarjeta o transferencia, debe existir una caja abierta.");
    }

    protected override void OnAccept()
    {
        var amount = ParseDecimal(_amount, "Monto", 0.01m);
        var method = ((Option<PaymentMethod>)_methodBox.SelectedItem!).Value;
        var description = string.IsNullOrWhiteSpace(_description.Text) ? null : _description.Text.Trim();

        Result = new RechargeRequest(amount, method, description);
        base.OnAccept();
    }
}

public sealed class CustomerHistoryDialog : FormDialog
{
    public CustomerHistoryDialog(CustomerDto customer, List<CustomerTransactionDto> transactions)
        : base($"Historial de {customer.Name}", "Últimos 200 movimientos de saldo del cliente.", width: 660)
    {
        AcceptButton.Content = "Cerrar";
        HideCancel();

        AddRow("Saldo actual", Ui.Money(customer.Balance), highlight: true);
        AddRow("Miembro", customer.IsMember ? "Sí" : "No");
        if (!string.IsNullOrWhiteSpace(customer.DocumentId))
            AddRow("Documento / RIF", customer.DocumentId!);
        AddSeparator();

        var grid = Kit.Table(
            Kit.Col("Fecha", nameof(CustomerTxRow.Date), 130),
            Kit.Col("Tipo", nameof(CustomerTxRow.Type), 110),
            Kit.Col("Monto USD", nameof(CustomerTxRow.Amount), 100, "{0:N2}"),
            Kit.Col("Saldo después", nameof(CustomerTxRow.Balance), 110, "{0:N2}"),
            Kit.Col("Descripción", nameof(CustomerTxRow.Description)));
        grid.ItemsSource = transactions.Select(t => new CustomerTxRow(t)).ToList();
        grid.Height = 300;

        Body.Children.Add(grid);

        if (transactions.Count == 0)
        {
            AddNote("Este cliente todavía no tiene movimientos de saldo.");
        }
    }
}

public sealed class CustomerTxRow
{
    public CustomerTxRow(CustomerTransactionDto transaction)
    {
        Date = transaction.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        Type = Labels.CustomerTxName(transaction.Type);
        Amount = transaction.Amount;
        Balance = transaction.BalanceAfter;
        Description = transaction.Description ?? "—";
    }

    public string Date { get; }
    public string Type { get; }
    public decimal Amount { get; }
    public decimal Balance { get; }
    public string Description { get; }
}
