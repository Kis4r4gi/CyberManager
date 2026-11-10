using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

/// <summary>Resumen del equipo y acciones rápidas sobre la sesión o el sistema.</summary>
public sealed class StationActionsDialog : FormDialog
{
    public string? Action { get; private set; }

    public StationActionsDialog(StationSummaryDto station, decimal exchangeRate,
        bool systemActions = true)
        : base(station.Name, $"Zona: {station.ZoneName ?? "sin zona"}")
    {
        AcceptButton.Content = "Cerrar";
        CancelButton.Visibility = Visibility.Collapsed;

        AddRow("Estado", Labels.StatusName(station.Status));

        if (station.SessionId is int sessionId)
        {
            AddRow("Sesión", $"#{sessionId} · {Labels.SessionTypeName(station.SessionType ?? SessionType.Postpaid)}");
            AddRow("Tiempo transcurrido", $"{station.ElapsedMinutes} min");
            AddRow(station.Overtime ? "Tiempo excedido" : "Tiempo restante",
                station.Overtime ? "Sí" : $"{Math.Max(station.RemainingMinutes, 0)} min");
            AddSeparator();
            AddRow("Total acumulado USD", $"$ {station.CurrentAmount:N2}", highlight: true);
            AddRow("Total acumulado Bs", $"Bs {station.CurrentAmount * exchangeRate:N2}");
        }
        else
        {
            AddRow("Disponibilidad", station.Status == StationStatus.Available
                ? "Listo para iniciar sesión"
                : Labels.StatusName(station.Status));
        }

        AddSeparator();
        Body.Children.Add(new TextBlock
        {
            Text = "ACCIONES",
            Style = Style("Label"),
            Margin = new Thickness(0, 2, 0, 8)
        });

        var actions = new UniformGrid { Columns = 2, Margin = new Thickness(0) };

        void Add(string label, string style, string action)
        {
            var button = new Button
            {
                Content = label,
                Style = Style(style),
                Margin = new Thickness(4),
                Padding = new Thickness(12, 9, 12, 9)
            };
            button.Click += (_, _) =>
            {
                Action = action;
                DialogResult = true;
                Close();
            };
            actions.Children.Add(Kit.WithTip(button, label));
        }

        if (station.SessionId is null)
        {
            if (station.Status != StationStatus.Maintenance)
                Add("Iniciar sesión", "PrimaryButton", "start");
        }
        else
        {
            Add("Cobrar y cerrar", "SuccessButton", "stop");
            Add("Extender tiempo", "BlueButton", "extend");
            Add(station.Status == StationStatus.Paused ? "Reanudar tiempo" : "Pausar tiempo",
                "WarningButton", "pause");
            Add("Transferir a otro PC", "GhostButton", "transfer");
        }

        if (systemActions)
        {
            Add("Ver pantalla remota", "BlueButton", "screen");
            if (station.Status == StationStatus.Offline)
                Add("Encender equipo (WoL)", "SuccessButton", "wake");
            Add(station.Status == StationStatus.Maintenance ? "Quitar mantenimiento" : "Poner en mantenimiento",
                "GhostButton", "maintenance");
            Add("Bloquear equipo", "GhostButton", "lock");
            Add("Enviar mensaje", "GhostButton", "message");

            // ---------- Control remoto del equipo ----------
            Add("Control de volumen", "GhostButton", "volume");
            Add("Silenciar", "GhostButton", "mute");
            Add("Activar sonido", "GhostButton", "unmute");
            Add("Apagar monitor", "GhostButton", "monitoroff");
            Add("Encender monitor", "GhostButton", "monitoron");
            Add("Bloquear teclado y mouse", "WarningButton", "lockinput");
            Add("Desbloquear entrada", "GhostButton", "unlockinput");
            Add("Cerrar programa", "WarningButton", "killprocess");

            Add("Apagar equipo", "DangerButton", "shutdown");
            Add("Reiniciar equipo", "DangerButton", "restart");
            Add("Cerrar sesión Windows", "DangerButton", "logoff");
        }

        Body.Children.Add(actions);
    }
}
