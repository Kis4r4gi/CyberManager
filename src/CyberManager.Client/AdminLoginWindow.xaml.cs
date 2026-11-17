using System.Windows;
using System.Windows.Input;
using CyberManager.Desktop.Api;
using CyberManager.Desktop.Services;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Client;

/// <summary>Inicio de sesión de administrador para abrir la configuración del cliente.</summary>
public partial class AdminLoginWindow : Window
{
    private bool _busy;

    /// <summary>Cliente autenticado con el token del administrador.</summary>
    public CyberApi? Api { get; private set; }

    public LoginResponse? Login { get; private set; }

    public AdminLoginWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            Loc.Apply(this);
            UsernameBox.Text = App.Config.LastUsername ?? string.Empty;
            if (UsernameBox.Text.Length == 0) UsernameBox.Focus();
            else PasswordBox.Focus();
        };
    }

    private void Password_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        _ = LoginAsync();
    }

    private void Login_Click(object sender, RoutedEventArgs e) => _ = LoginAsync();

    private async Task LoginAsync()
    {
        if (_busy) return;

        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;
        if (username.Length == 0 || password.Length == 0)
        {
            ShowError(L.T("Indique el usuario y la contraseña."));
            return;
        }

        _busy = true;
        LoginButton.IsEnabled = false;
        ErrorText.Visibility = Visibility.Collapsed;

        try
        {
            var api = new CyberApi(App.Api?.BaseUrl ?? App.Config.ServerUrl);
            var login = await api.LoginAsync(username, password);

            if (login.Role != UserRole.Admin)
            {
                ShowError(L.T("Se requiere una cuenta con rol de administrador."));
                return;
            }

            App.Config.LastUsername = login.Username;
            App.Config.Save("Client");

            Api = api;
            Login = login;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _busy = false;
            LoginButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
