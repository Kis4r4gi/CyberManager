using System.Windows;
using System.Windows.Input;
using CyberManager.Admin.Services;
using CyberManager.Desktop.Api;
using CyberManager.Desktop.Services;
using CyberManager.Shared;

namespace CyberManager.Admin;

public partial class LoginWindow : Window
{
    private readonly string _placementKey;

    public LoginWindow() : this(null)
    {
    }

    /// <summary>
    /// Inicia sesión con un usuario preseleccionado, útil al cambiar de cajero
    /// desde el menú de sesión de la consola.
    /// </summary>
    public LoginWindow(string? username)
    {
        InitializeComponent();

        // La ventana de inicio de sesión se recuerda por usuario de Windows.
        _placementKey = "login:" + Environment.UserName;

        var config = AppConfig.Load("Admin");
        ServerUrlBox.Text = config.ServerUrl;
        TerminalBox.Text = ApiClient.NormalizeTerminal(config.TerminalName);
        UsernameBox.Text = string.IsNullOrWhiteSpace(username)
            ? config.LastUsername ?? "admin"
            : username;

        Loaded += (_, _) =>
        {
            Loc.Apply(this);
            Title = $"{L.T("CyberManager · Iniciar sesión")} · v{AppInfo.Version}";
            if (string.IsNullOrWhiteSpace(UsernameBox.Text)) UsernameBox.Focus();
            else PasswordBox.Focus();
        };

        Closed += (_, _) => WindowPlacementStore.SaveWindow(_placementKey, this);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeWindow.ApplyDarkTitleBar(this);
        NativeWindow.RestorePlacement(this, WindowPlacementStore.Load(_placementKey));
    }

    private void Password_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Login_Click(sender, e);
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsernameBox.Text) || string.IsNullOrWhiteSpace(PasswordBox.Password))
        {
            ShowError("Indique el usuario y la contraseña.");
            return;
        }

        ErrorText.Visibility = Visibility.Collapsed;
        LoginButton.IsEnabled = false;
        LoginButton.Content = L.T("Conectando...");

        try
        {
            var api = new CyberApi(ServerUrlBox.Text);
            var terminal = ApiClient.NormalizeTerminal(TerminalBox.Text);
            api.Headers[ApiClient.TerminalHeader] = terminal;
            var response = await api.LoginAsync(UsernameBox.Text.Trim(), PasswordBox.Password);

            var url = ApiClient.Normalize(ServerUrlBox.Text);
            App.SetSession(api, response, url, terminal);

            var config = AppConfig.Load("Admin");
            config.ServerUrl = url;
            config.TerminalName = terminal;
            config.LastUsername = response.Username;
            config.Save("Admin");

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Content = L.T("Entrar a la consola");
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
