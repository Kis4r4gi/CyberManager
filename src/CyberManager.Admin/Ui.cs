using System.Windows;

namespace CyberManager.Admin;

/// <summary>Vista que puede recargar sus datos al navegar.</summary>
public interface IRefreshable
{
    Task RefreshAsync();
}

public static class Ui
{
    public static async Task Run(Func<Task> action, string title = "Error")
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Error(ex.Message, title);
        }
    }

    public static void Error(string message, string title = "Error")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public static void Info(string message, string title = "CyberManager")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public static bool Confirm(string message, string title = "Confirmar")
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public static string Money(decimal usd) => $"$ {usd:N2}";

    public static string MoneyBs(decimal ves) => $"Bs {ves:N2}";
}
