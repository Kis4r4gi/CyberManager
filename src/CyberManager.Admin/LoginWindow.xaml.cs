using System.Windows;
using System.Windows.Input;
using CyberManager.Desktop.Api;
using CyberManager.Desktop.Services;
using CyberManager.Shared;

namespace CyberManager.Admin;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();

        var config = AppConfig.Load("Admin");
        ServerUrlBox.Text = config.ServerUrl;
        UsernameBox.Text = config.LastUsername ?? "admin";

        Loaded += (_, _) =>
        {
            Loc.Apply(this);
            if (string.IsNullOrWhiteSpace(UsernameBox.Text)) UsernameBox.Focus();
            else PasswordBox.Focus();
        };
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
            var response = await api.LoginAsync(UsernameBox.Text.Trim(), PasswordBox.Password);

            var url = ApiClient.Normalize(ServerUrlBox.Text);
            App.SetSession(api, response, url);

            var config = AppConfig.Load("Admin");
            config.ServerUrl = url;
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
