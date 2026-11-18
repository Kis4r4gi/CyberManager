using System.Net.Http;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;
using CyberManager.Client.Services;
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

    /// <summary>
    /// La contraseña se validó con la credencial guardada en el equipo porque el
    /// servidor no responde. La consola no puede abrir la configuración remota.
    /// </summary>
    public bool Offline { get; private set; }

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
            var url = ServerLocator.Normalize(App.Api?.BaseUrl ?? App.Config.ServerUrl);
            CyberApi api;
            LoginResponse login;

            try
            {
                // Intento corto: si el servidor cambió de IP conviene descubrirlo
                // enseguida en lugar de esperar el tiempo máximo de HTTP.
                api = new CyberApi(url, TimeSpan.FromSeconds(8));
                login = await api.LoginAsync(username, password);
            }
            catch (Exception ex) when (IsConnectionError(ex))
            {
                // Puede que el servidor tenga otra IP: se busca en la red y se
                // reintenta una sola vez antes de recurrir a la credencial local.
                var found = await ServerLocator.FindWorkingAsync(url);
                if (found is null) throw;

                url = ServerLocator.Normalize(found);
                api = new CyberApi(url);
                login = await api.LoginAsync(username, password);

                App.Config.ServerUrl = url;
                App.Config.Save("Client");
                ClientLog.Info($"Login de administrador: servidor encontrado en {url}.");
            }

            if (login.Role != UserRole.Admin)
            {
                ShowError(L.T("Se requiere una cuenta con rol de administrador."));
                return;
            }

            // La credencial local solo se siembra con un login válido del servidor.
            AdminCredentialCache.Seed(login.Username, password);

            App.Config.LastUsername = login.Username;
            App.Config.Save("Client");

            Api = api;
            Login = login;
            DialogResult = true;
            Close();
        }
        catch (Exception ex) when (IsConnectionError(ex))
        {
            // Sin red no se puede preguntar al servidor: se usa la copia local.
            switch (AdminCredentialCache.Verify(username, password))
            {
                case AdminCredentialCache.Result.Ok:
                    Offline = true;
                    Api = null;
                    Login = null;
                    App.Config.LastUsername = username;
                    App.Config.Save("Client");
                    DialogResult = true;
                    Close();
                    break;

                case AdminCredentialCache.Result.LockedOut:
                    ShowError(L.T("Demasiados intentos fallidos. Espere unos minutos."));
                    break;

                case AdminCredentialCache.Result.Invalid:
                    ShowError(L.T("Usuario o contraseña incorrectos."));
                    break;

                default:
                    ShowError(L.T("El servidor no responde y este equipo no tiene una " +
                                  "credencial de administrador guardada. Conecte la red e intente de nuevo."));
                    break;
            }
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

    /// <summary>
    /// Distingue un fallo de red (servidor apagado, IP cambiada, cable desconectado)
    /// de un rechazo del servidor. Un 401 nunca debe abrir la puerta local.
    /// </summary>
    private static bool IsConnectionError(Exception ex) => ServerLocator.IsConnectionError(ex);

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
