using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CyberManager.Desktop.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Client;

/// <summary>Fila de un paquete de tiempo lista para mostrar en la lista de compra.</summary>
public sealed class PrepaidPackageRow
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string MinutesText { get; init; } = string.Empty;
    public string PriceText { get; init; } = string.Empty;
    public string PriceBsText { get; init; } = string.Empty;
    public bool CanBuy { get; init; } = true;
}

/// <summary>
/// Autoservicio prepago de la pantalla de bloqueo: crear cuenta, iniciar sesión,
/// comprar paquetes de tiempo y abrir la sesión del equipo.
/// </summary>
public partial class PrepaidWindow : Window
{
    private PrepaidInfoDto? _info;
    private PrepaidAccountDto? _account;
    private string? _password;
    private int? _activeSessionId;
    private int _activeSessionRemaining;
    private bool _busy;

    /// <summary>La sesión prepago se inició correctamente; la aplicación debe refrescar el estado.</summary>
    public event Action? SessionStarted;

    public PrepaidWindow()
    {
        InitializeComponent();
        FitToWorkArea();
        SizeChanged += (_, _) => CenterInWorkArea();

        Loaded += async (_, _) =>
        {
            Loc.Apply(this);
            ShowLoginPanel();
            await LoadInfoAsync();
        };
    }

    /// <summary>
    /// Ajusta la ventana al área de trabajo del monitor principal para que el formulario
    /// no quede recortado en pantallas pequeñas (por ejemplo 1366x768).
    /// </summary>
    private void FitToWorkArea()
    {
        var area = SystemParameters.WorkArea;
        var maxWidth = Math.Max(MinWidth, area.Width - 32);
        var maxHeight = Math.Max(MinHeight, area.Height - 32);

        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
        Width = Math.Min(Width, maxWidth);
    }

    /// <summary>Mantiene la ventana centrada y completa dentro del área de trabajo al cambiar de panel.</summary>
    private void CenterInWorkArea()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var area = SystemParameters.WorkArea;
        Left = area.Left + Math.Max(0, (area.Width - ActualWidth) / 2);
        Top = area.Top + Math.Max(0, (area.Height - ActualHeight) / 2);
    }

    private async Task LoadInfoAsync()
    {
        if (App.Api is null)
        {
            SetStatus(L.T("Sin conexión con el servidor. Avise al administrador."), true);
            return;
        }

        try
        {
            _info = await App.Api.PrepaidInfoAsync();
            BusinessNameText.Text = _info.BusinessName;
            RenderPackages();
            LoadingPanel.Visibility = Visibility.Collapsed;
            AuthPanel.Visibility = Visibility.Visible;
            SetStatus(L.T("Ingrese con su cuenta o cree una nueva."));
        }
        catch (Exception ex)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            AuthPanel.Visibility = Visibility.Visible;
            SetStatus(ex.Message, true);
        }
    }

    // ============================================================
    //  Navegación entre paneles
    // ============================================================
    private void ShowLoginPanel()
    {
        AuthPanel.Visibility = Visibility.Visible;
        AccountPanel.Visibility = Visibility.Collapsed;
        LoginPanel.Visibility = Visibility.Visible;
        RegisterPanel.Visibility = Visibility.Collapsed;
        LoginTabButton.Style = (Style)FindResource("PrimaryButton");
        RegisterTabButton.Style = (Style)FindResource("GhostButton");
        LoginErrorText.Visibility = Visibility.Collapsed;
        RegisterErrorText.Visibility = Visibility.Collapsed;
    }

    private void ShowRegisterPanel()
    {
        AuthPanel.Visibility = Visibility.Visible;
        AccountPanel.Visibility = Visibility.Collapsed;
        LoginPanel.Visibility = Visibility.Collapsed;
        RegisterPanel.Visibility = Visibility.Visible;
        RegisterTabButton.Style = (Style)FindResource("PrimaryButton");
        LoginTabButton.Style = (Style)FindResource("GhostButton");
        LoginErrorText.Visibility = Visibility.Collapsed;
        RegisterErrorText.Visibility = Visibility.Collapsed;
    }

    private void ShowAccountPanel()
    {
        if (_account is null) return;

        LoadingPanel.Visibility = Visibility.Collapsed;
        AuthPanel.Visibility = Visibility.Collapsed;
        AccountPanel.Visibility = Visibility.Visible;

        GreetingText.Text = L.IsEnglish ? $"Hello, {_account.Name}" : $"Hola, {_account.Name}";
        AccountMinutesText.Text = $"{_account.PrepaidMinutes} min";
        AccountBalanceText.Text = $"$ {_account.Balance:N2}";
        var rate = _info?.ExchangeRate ?? 0m;
        AccountBalanceBsText.Text = $"Bs {_account.Balance * rate:N2}";

        MinutesBox.Text = Math.Max(_account.PrepaidMinutes, 0).ToString();

        if (_activeSessionId is not null)
        {
            SessionNoteBorder.Visibility = Visibility.Visible;
            SessionNoteText.Text = L.IsEnglish
                ? $"You have an active session on this station ({Math.Max(_activeSessionRemaining, 0)} min left). Any package you buy is added to the session."
                : $"Tiene una sesión activa en este equipo ({Math.Max(_activeSessionRemaining, 0)} min restantes). Los paquetes que compre se suman a la sesión.";
            StartPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            SessionNoteBorder.Visibility = Visibility.Collapsed;
            StartPanel.Visibility = Visibility.Visible;

            if (_account.PrepaidMinutes <= 0)
            {
                StartHintText.Text = L.T("Compre un paquete de tiempo para poder entrar al equipo.");
                StartHintText.Visibility = Visibility.Visible;
                StartButton.IsEnabled = false;
            }
            else
            {
                StartHintText.Visibility = Visibility.Collapsed;
                StartButton.IsEnabled = true;
            }
        }

        RenderPackages();
    }

    private void RenderPackages()
    {
        if (_info is null || _info.Packages.Count == 0)
        {
            PackageList.ItemsSource = null;
            PackagesEmptyText.Visibility = Visibility.Visible;
            return;
        }

        PackagesEmptyText.Visibility = Visibility.Collapsed;
        PackageList.ItemsSource = _info.Packages.Select(p =>
        {
            var total = p.Minutes + p.BonusMinutes;
            var minutes = p.BonusMinutes > 0
                ? L.IsEnglish
                    ? $"{total} min ({p.Minutes} + {p.BonusMinutes} bonus)"
                    : $"{total} min ({p.Minutes} + {p.BonusMinutes} de bono)"
                : $"{total} min";

            return new PrepaidPackageRow
            {
                Id = p.Id,
                Name = p.Name,
                MinutesText = minutes,
                PriceText = $"$ {p.Price:N2}",
                PriceBsText = $"Bs {p.Price * _info.ExchangeRate:N2}",
                CanBuy = _account is null || _account.Balance >= p.Price
            };
        }).ToList();
    }

    // ============================================================
    //  Acciones
    // ============================================================
    private void LoginTab_Click(object sender, RoutedEventArgs e) => ShowLoginPanel();

    private void RegisterTab_Click(object sender, RoutedEventArgs e) => ShowRegisterPanel();

    private void ShowRegister_Click(object sender, RoutedEventArgs e) => ShowRegisterPanel();

    private void ShowLogin_Click(object sender, RoutedEventArgs e) => ShowLoginPanel();

    private void Password_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = LoginAsync();
        }
    }

    private async void Login_Click(object sender, RoutedEventArgs e) => await LoginAsync();

    private async Task LoginAsync()
    {
        if (_busy) return;

        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;
        if (username.Length == 0 || password.Length == 0)
        {
            ShowLoginError(L.T("Indique el usuario y la contraseña."));
            return;
        }

        await RunAsync(async () =>
        {
            var response = await App.Api!.PrepaidLoginAsync(new PrepaidLoginRequest(username, password));
            _account = response.Account;
            _password = password;
            _activeSessionId = response.ActiveSessionId;
            _activeSessionRemaining = response.ActiveSessionRemainingMinutes;
            ShowAccountPanel();
            SetStatus(L.T("Sesión iniciada."));
        }, ShowLoginError);
    }

    private async void Register_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        var name = RegisterNameBox.Text.Trim();
        var username = RegisterUsernameBox.Text.Trim();
        var password = RegisterPasswordBox.Password;
        var confirm = RegisterConfirmBox.Password;

        if (name.Length < 2) { ShowRegisterError(L.T("Escriba su nombre completo.")); return; }
        if (username.Length < 3) { ShowRegisterError(L.T("El usuario debe tener al menos 3 caracteres.")); return; }
        if (password.Length < 4) { ShowRegisterError(L.T("La contraseña debe tener al menos 4 caracteres.")); return; }
        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            ShowRegisterError(L.T("Las contraseñas no coinciden."));
            return;
        }

        await RunAsync(async () =>
        {
            var account = await App.Api!.PrepaidRegisterAsync(new PrepaidRegisterRequest(
                name, username, password, RegisterPhoneBox.Text.Trim(), null));
            _account = account;
            _password = password;
            _activeSessionId = null;
            _activeSessionRemaining = 0;
            ShowAccountPanel();
            SetStatus(L.T("Cuenta creada. Compre un paquete de tiempo para empezar."));
        }, ShowRegisterError);
    }

    private async void BuyPackage_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _account is null || _password is null || _info is null) return;
        if (sender is not FrameworkElement { Tag: int packageId }) return;

        var package = _info.Packages.FirstOrDefault(p => p.Id == packageId);
        if (package is null) return;

        var question = L.IsEnglish
            ? $"Buy \"{package.Name}\" for $ {package.Price:N2}?"
            : $"¿Comprar «{package.Name}» por $ {package.Price:N2}?";
        if (MessageBox.Show(this, question, L.T("Confirmar compra"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunAsync(async () =>
        {
            var result = await App.Api!.PrepaidRechargeAsync(new PrepaidRechargeRequest(
                _account.Username, _password, packageId));
            _account = result.Account;
            if (result.ExtendedSession) _activeSessionRemaining += result.AddedMinutes;

            ShowAccountPanel();
            SetStatus(result.ExtendedSession
                ? (L.IsEnglish
                    ? $"{result.PackageName}: +{result.AddedMinutes} min added to your session."
                    : $"«{result.PackageName}»: +{result.AddedMinutes} min sumados a su sesión.")
                : (L.IsEnglish
                    ? $"{result.PackageName}: +{result.AddedMinutes} min available."
                    : $"«{result.PackageName}»: +{result.AddedMinutes} min disponibles."));
        }, message => SetStatus(message, true));
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _account is null || _password is null) return;

        if (!int.TryParse(MinutesBox.Text.Trim(), out var minutes) || minutes <= 0)
        {
            ShowStartHint(L.T("Indique cuántos minutos desea usar."));
            return;
        }

        if (minutes > _account.PrepaidMinutes)
        {
            ShowStartHint(L.IsEnglish
                ? $"You only have {_account.PrepaidMinutes} min available."
                : $"Solo tiene {_account.PrepaidMinutes} min disponibles.");
            return;
        }

        await RunAsync(async () =>
        {
            var result = await App.Api!.PrepaidStartAsync(new PrepaidStartRequest(
                _account.Username, _password, minutes));
            _account = result.Account;
            SetStatus(L.IsEnglish
                ? $"Session started with {result.UsedMinutes} min. Have fun!"
                : $"Sesión iniciada con {result.UsedMinutes} min. ¡Buen juego!");
            SessionStarted?.Invoke();
            Close();
        }, ShowStartHint);
    }

    private void SwitchAccount_Click(object sender, RoutedEventArgs e)
    {
        _account = null;
        _password = null;
        _activeSessionId = null;
        _activeSessionRemaining = 0;
        PasswordBox.Password = string.Empty;
        ShowLoginPanel();
        SetStatus(L.T("Ingrese con otra cuenta."));
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ============================================================
    //  Utilidades
    // ============================================================
    private async Task RunAsync(Func<Task> action, Action<string> showError)
    {
        if (_busy) return;
        _busy = true;
        StartButton.IsEnabled = false;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            showError(ex.Message);
        }
        finally
        {
            _busy = false;
            if (_account is not null && _activeSessionId is null)
                StartButton.IsEnabled = _account.PrepaidMinutes > 0;
        }
    }

    private void ShowLoginError(string message)
    {
        LoginErrorText.Text = message;
        LoginErrorText.Visibility = Visibility.Visible;
    }

    private void ShowRegisterError(string message)
    {
        RegisterErrorText.Text = message;
        RegisterErrorText.Visibility = Visibility.Visible;
    }

    private void ShowStartHint(string message)
    {
        StartHintText.Text = message;
        StartHintText.Visibility = Visibility.Visible;
    }

    private void SetStatus(string message, bool error = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource(error ? "DangerBrush" : "MutedBrush");
    }
}
