using System.Windows;
using System.Windows.Controls;
using CyberManager.Admin.Views;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin;

/// <summary>Fila del diálogo de escaneo: permite elegir qué equipos se dan de alta.</summary>
public sealed class NetworkScanRow
{
    public bool IsSelected { get; set; }
    public string Equipo { get; init; } = string.Empty;
    public string Rol { get; init; } = string.Empty;
    public string Ip { get; init; } = string.Empty;
    public string Mac { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public DiscoveredStationDto Source { get; init; } = null!;
}

/// <summary>
/// Muestra las estaciones encontradas por el escaneo de red. El administrador elige
/// cuáles agregar o actualizar; al confirmar, el servidor les envía el ID y el token.
/// </summary>
public sealed class NetworkScanDialog : FormDialog
{
    private readonly List<NetworkScanRow> _rows;

    /// <summary>Equipos marcados por el administrador.</summary>
    public List<DiscoveryApplyItem> Selected { get; } = new();

    public NetworkScanDialog(List<DiscoveredStationDto> found)
        : base("Equipos encontrados en la red",
            "Seleccione los equipos que desea agregar o actualizar. Al confirmar, el servidor " +
            "les envía su ID y token para que queden conectados automáticamente.",
            "Agregar seleccionados", 860)
    {
        _rows = found.Select(item => new NetworkScanRow
        {
            IsSelected = true,
            Equipo = item.MachineName,
            Rol = string.Equals(item.Role, "agent", StringComparison.OrdinalIgnoreCase)
                ? "Agente"
                : "Cliente",
            Ip = item.IpAddress,
            Mac = item.MacAddress,
            Version = string.IsNullOrWhiteSpace(item.Version) ? "—" : item.Version,
            Estado = item.IsNew
                ? "Nuevo"
                : item.NeedsToken
                    ? "Actualiza token"
                    : "Ya registrado",
            Source = item
        }).ToList();

        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8)
        };
        tools.Children.Add(Kit.Button("Marcar todos", "GhostButton", () => SetAll(true)));
        tools.Children.Add(Kit.Button("Quitar marcas", "GhostButton", () => SetAll(false)));
        Body.Children.Add(tools);

        var grid = new DataGrid
        {
            ItemsSource = _rows,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserReorderColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            SelectionMode = DataGridSelectionMode.Single,
            Height = 320,
            Background = Kit.Brush("SurfaceBrush"),
            RowBackground = Kit.Brush("SurfaceBrush"),
            AlternatingRowBackground = Kit.Brush("SurfaceBrush"),
            Foreground = Kit.Brush("TextBrush"),
            BorderBrush = Kit.Brush("BorderBrush"),
            BorderThickness = new Thickness(1)
        };

        grid.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "Incluir",
            Binding = new System.Windows.Data.Binding(nameof(NetworkScanRow.IsSelected))
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            },
            Width = 70
        });
        grid.Columns.Add(Kit.AutoCol("Equipo", nameof(NetworkScanRow.Equipo), 130));
        grid.Columns.Add(Kit.AutoCol("Rol", nameof(NetworkScanRow.Rol), 80));
        grid.Columns.Add(Kit.AutoCol("Dirección IP", nameof(NetworkScanRow.Ip), 110));
        grid.Columns.Add(Kit.AutoCol("MAC", nameof(NetworkScanRow.Mac), 140));
        grid.Columns.Add(Kit.AutoCol("Versión", nameof(NetworkScanRow.Version), 80));
        grid.Columns.Add(Kit.AutoCol("Estado", nameof(NetworkScanRow.Estado), 110));

        Body.Children.Add(grid);

        AddNote("El equipo debe estar encendido y con CyberManager abierto o el agente " +
                "instalado. Si el firewall bloquea los puertos UDP 5092 y 5093, el escaneo " +
                "no lo encontrará.");

        if (_rows.Count == 0)
            AddNote("No se encontró ningún equipo en la red local.", "WarningBrush");
    }

    private void SetAll(bool selected)
    {
        foreach (var row in _rows) row.IsSelected = selected;

        // Fuerza al DataGrid a releer los valores del origen.
        if (Body.Children.OfType<DataGrid>().FirstOrDefault() is { } grid)
        {
            grid.ItemsSource = null;
            grid.ItemsSource = _rows;
        }
    }

    protected override void OnAccept()
    {
        Selected.Clear();
        Selected.AddRange(_rows.Where(row => row.IsSelected).Select(row => new DiscoveryApplyItem(
            row.Source.Role,
            row.Source.StationId,
            row.Source.Token,
            row.Source.MachineName,
            row.Source.IpAddress,
            row.Source.MacAddress,
            row.Source.Version)));

        if (Selected.Count == 0)
        {
            ShowError("Seleccione al menos un equipo de la lista.");
            return;
        }

        DialogResult = true;
    }
}
