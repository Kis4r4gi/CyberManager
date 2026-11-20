using System.Windows;
using System.Windows.Controls;
using CyberManager.Shared;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

/// <summary>
/// Sección única del servidor para administrar fondos y videos promocionales. La
/// barra superior concentra las acciones compartidas en menús desplegables y la
/// configuración de reproducción queda siempre visible, sin depender de la pestaña.
/// </summary>
public sealed class MediaView : UserControl, IRefreshable
{
    private static readonly Option<WallpaperOrder>[] Orders =
    {
        new(WallpaperOrder.Ordered, "Ordenado"),
        new(WallpaperOrder.Random, "Aleatorio")
    };

    private static readonly Option<WallpaperTransition>[] Transitions =
    {
        new(WallpaperTransition.None, "Sin transición"),
        new(WallpaperTransition.Fade, "Fundido"),
        new(WallpaperTransition.Slide, "Deslizamiento"),
        new(WallpaperTransition.Zoom, "Zoom")
    };

    private readonly WallpapersView _wallpapers = new();
    private readonly PromosView _videos = new();
    private readonly TabItem _wallpapersTab = new() { Header = "Fondos" };
    private readonly TabItem _videosTab = new() { Header = "Videos promocionales" };
    private readonly TabControl _tabs = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBox _secondsBox = Kit.Input("30", "Entre 5 y 3600 segundos");
    private readonly ComboBox _orderCombo = Kit.Combo(Orders);
    private readonly ComboBox _transitionCombo = Kit.Combo(Transitions);
    private readonly TextBlock _settingsStatus = Kit.Muted("Cargando configuración...");
    private bool _loading;

    public MediaView()
    {
        BuildLayout();
        Loaded += async (_, _) => await Ui.Run(RefreshAsync, "No se pudo cargar la sección multimedia");
    }

    private void BuildLayout()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(Kit.PageHeader("Fondos y videos",
            "Todo el contenido multimedia de los terminales en un solo lugar: agregar, publicar, configurar y abrir la caché.",
            Kit.MenuButton("Agregar", "PrimaryButton",
                ("Subir imágenes", () => _wallpapers.Upload()),
                ("Buscar en internet", () => _wallpapers.SearchOnInternet()),
                ("Sincronizar carpeta de fondos", () => _ = _wallpapers.SyncExchangeFolderAsync()),
                ("Subir videos", () => _videos.Upload())),
            Kit.MenuButton("Enviar a terminales", "GhostButton",
                ("Reproducir fondo aleatorio", PlayRandomNow),
                ("Actualizar archivos en clientes", () => _videos.RefreshClients()),
                ("Abrir carpeta de videos en terminales", () => _videos.OpenCacheFolder())),
            Kit.MenuButton("Configuración", "GhostButton",
                ("Guardar y aplicar cambios", ApplySettings),
                ("Recargar configuración", () => _ = LoadSettingsAsync())),
            Kit.MenuButton("Herramientas", "GhostButton",
                ("Requisitos de codificación", () => PromosView.ShowRequirements()),
                ("Abrir carpeta de fondos (consola)", () => _wallpapers.OpenExchangeFolder()),
                ("Actualizar vista", () => _ = RefreshAsync()))));

        var settings = BuildSettingsBar();
        Grid.SetRow(settings, 1);
        root.Children.Add(settings);

        _wallpapersTab.Content = _wallpapers;
        _videosTab.Content = _videos;
        _tabs.Items.Add(_wallpapersTab);
        _tabs.Items.Add(_videosTab);
        foreach (TabItem tab in _tabs.Items)
            tab.Padding = new Thickness(14, 8, 14, 8);

        _tabs.SelectionChanged += OnTabChanged;

        Grid.SetRow(_tabs, 2);
        root.Children.Add(_tabs);
        Content = root;
    }

    private Border BuildSettingsBar()
    {
        _secondsBox.Width = 86;
        _orderCombo.Width = 140;
        _transitionCombo.Width = 150;
        _secondsBox.LostFocus += (_, _) => NormalizeSeconds();

        var title = new TextBlock
        {
            Text = L.T("CONFIGURACIÓN DEL SERVIDOR"),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Kit.Brush("AccentBrush"),
            Margin = new Thickness(0, 0, 0, 6)
        };

        var fields = Kit.HStack(18,
            Kit.Field("Segundos entre imágenes", _secondsBox),
            Kit.Field("Orden", _orderCombo),
            Kit.Field("Transición", _transitionCombo),
            Kit.Button("Guardar y aplicar", "PrimaryButton", ApplySettings));

        _settingsStatus.TextWrapping = TextWrapping.Wrap;
        var content = new StackPanel();
        content.Children.Add(title);
        content.Children.Add(fields);
        content.Children.Add(_settingsStatus);

        return Kit.Card(content, 14, new Thickness(0, 0, 0, 8));
    }

    public async Task RefreshAsync()
    {
        _loading = true;
        try
        {
            if (ReferenceEquals(_tabs.SelectedItem, _videosTab))
                await _videos.RefreshAsync();
            else
                await _wallpapers.RefreshAsync();

            await LoadSettingsAsync();
        }
        finally
        {
            _loading = false;
        }
    }

    private async void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;

        if (ReferenceEquals(_tabs.SelectedItem, _videosTab))
            await Ui.Run(_videos.RefreshAsync, "No se pudo cargar los videos");
        else
            await Ui.Run(_wallpapers.RefreshAsync, "No se pudo cargar los fondos");
    }

    private async Task LoadSettingsAsync()
    {
        var playlist = await App.Api.WallpaperPlaylistAsync();
        UpdateSettings(playlist);
    }

    private void UpdateSettings(WallpaperPlaylistDto? playlist)
    {
        var seconds = playlist?.RotationSeconds ?? WallpaperSettings.DefaultRotationSeconds;
        var order = playlist?.Order ?? WallpaperOrder.Ordered;
        var transition = playlist?.Transition ?? WallpaperTransition.Fade;

        _secondsBox.Text = seconds.ToString();
        _orderCombo.SelectedIndex = Array.FindIndex(Orders, o => o.Value == order);
        _transitionCombo.SelectedIndex = Array.FindIndex(Transitions, t => t.Value == transition);
        if (_orderCombo.SelectedIndex < 0) _orderCombo.SelectedIndex = 0;
        if (_transitionCombo.SelectedIndex < 0) _transitionCombo.SelectedIndex = 1;

        _settingsStatus.Text =
            $"Los fondos rotan cada {seconds} segundo(s), en orden " +
            $"{WallpaperSettings.OrderText(order).ToLowerInvariant()} y con transición " +
            $"{WallpaperSettings.TransitionText(transition).ToLowerInvariant()}. " +
            "Los cambios se guardan en el servidor y los equipos los aplican al consultar el estado.";
    }

    private async void ApplySettings()
    {
        if (_loading) return;
        if (ParseSeconds() is not int seconds) return;

        var order = SelectedOrder();
        var transition = SelectedTransition();

        await Ui.Run(async () =>
        {
            var playlist = await App.Api.ApplyWallpaperAsync(
                new ApplyWallpaperSettingsRequest(seconds, order, transition));
            UpdateSettings(playlist);
            Ui.Info($"Configuración actualizada: cada {seconds} segundo(s), " +
                    $"{WallpaperSettings.OrderText(order).ToLowerInvariant()} y " +
                    $"{WallpaperSettings.TransitionText(transition).ToLowerInvariant()}.",
                "Fondos y videos");
        }, "No se pudo aplicar la configuración");
    }

    /// <summary>
    /// Orden inmediato a las terminales activas. No modifica la configuración guardada.
    /// </summary>
    private async void PlayRandomNow()
    {
        if (_loading) return;
        if (!Ui.Confirm("¿Mostrar un fondo aleatorio ahora en todos los equipos activos?\n\n" +
                        "La configuración guardada no cambiará.", "Reproducir aleatoriamente"))
            return;

        await Ui.Run(async () =>
        {
            var stations = (await App.Api.StationsAsync()).Where(s => s.Active).ToList();
            foreach (var station in stations)
                await App.Api.CommandAsync(station.Id, AgentCommandType.PlayRandomWallpaper);

            Ui.Info(stations.Count == 0
                    ? "No hay equipos activos para enviar la orden."
                    : $"Orden enviada a {stations.Count} equipo(s).",
                "Reproducir aleatoriamente");
        }, "No se pudo enviar la orden");
    }

    private WallpaperOrder SelectedOrder()
        => _orderCombo.SelectedItem is Option<WallpaperOrder> option
            ? option.Value
            : WallpaperOrder.Ordered;

    private WallpaperTransition SelectedTransition()
        => _transitionCombo.SelectedItem is Option<WallpaperTransition> option
            ? option.Value
            : WallpaperTransition.Fade;

    private int? ParseSeconds()
    {
        if (!int.TryParse(_secondsBox.Text.Trim(), out var seconds))
        {
            Ui.Error($"Indique un número de segundos entre {WallpaperSettings.MinRotationSeconds} y " +
                     $"{WallpaperSettings.MaxRotationSeconds}.");
            return null;
        }

        if (seconds < WallpaperSettings.MinRotationSeconds || seconds > WallpaperSettings.MaxRotationSeconds)
        {
            Ui.Error($"La rotación debe estar entre {WallpaperSettings.MinRotationSeconds} y " +
                     $"{WallpaperSettings.MaxRotationSeconds} segundos.");
            return null;
        }

        _secondsBox.Text = seconds.ToString();
        return seconds;
    }

    private void NormalizeSeconds()
    {
        if (int.TryParse(_secondsBox.Text.Trim(), out var seconds))
            _secondsBox.Text = Math.Clamp(seconds, WallpaperSettings.MinRotationSeconds,
                WallpaperSettings.MaxRotationSeconds).ToString();
        else
            _secondsBox.Text = WallpaperSettings.DefaultRotationSeconds.ToString();
    }
}
