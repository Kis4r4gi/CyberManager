using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using CyberManager.Shared;

namespace CyberManager.Admin.Views;

/// <summary>Constructores de interfaz compartidos por las vistas de la consola.</summary>
public static class Kit
{
    public static Style Style(string key) => (Style)Application.Current.FindResource(key);
    public static Brush Brush(string key) => (Brush)Application.Current.FindResource(key);

    /// <summary>Descripción breve de cada botón, mostrada tras mantener el cursor 2 segundos.</summary>
    private static readonly Dictionary<string, string> ButtonTips = new(StringComparer.OrdinalIgnoreCase)
    {
        ["↑ Ascendente"] = "Cambia el orden de la lista.",
        ["↓ Descendente"] = "Cambia el orden de la lista.",
        ["+"] = "Aumenta la cantidad.",
        ["-"] = "Disminuye la cantidad.",
        ["−"] = "Disminuye la cantidad.",
        ["✕"] = "Quita el producto del pedido.",
        ["?"] = "Muestra la ayuda de esta pantalla.",
        ["Abonar"] = "Registra un abono a la cuenta del cliente.",
        ["Abrir caja"] = "Abre la caja del turno actual.",
        ["Abrir carpeta"] = "Abre la carpeta donde se guardó el archivo.",
        ["Aceptar"] = "Guarda los datos del formulario.",
        ["Activar / desactivar"] = "Activa o desactiva el registro seleccionado.",
        ["Activar sonido"] = "Activa el audio del equipo.",
        ["Actualizar"] = "Vuelve a cargar la información desde el servidor.",
        ["Actualizar lista"] = "Actualiza la lista con los datos más recientes.",
        ["Agregar"] = "Añade el elemento a la lista.",
        ["Apagar equipo"] = "Apaga el equipo.",
        ["Apagar monitor"] = "Apaga el monitor del equipo.",
        ["Aplicar y guardar ahora"] = "Aplica los cambios y los guarda de inmediato.",
        ["Bajar"] = "Mueve el elemento una posición hacia abajo.",
        ["Bloquear"] = "Bloquea el acceso del equipo.",
        ["Bloquear equipo"] = "Bloquea la pantalla del equipo.",
        ["Bloquear teclado y mouse"] = "Bloquea el teclado y el mouse del equipo.",
        ["Buscar actualizaciones ahora"] = "Consulta GitHub si hay una versión nueva.",
        ["Cancelar"] = "Cierra sin guardar cambios.",
        ["Cancelar reserva"] = "Anula la reserva seleccionada.",
        ["Capturar ahora"] = "Toma una captura de la pantalla remota.",
        ["Cerrar"] = "Cierra la ventana.",
        ["Cerrar caja"] = "Cierra la caja y muestra el resumen del turno.",
        ["Cerrar programa"] = "Cierra el programa indicado en el equipo.",
        ["Cerrar sesión Windows"] = "Cierra la sesión de Windows del equipo.",
        ["Cierre diario"] = "Abre el cierre de caja del día.",
        ["Cobrar"] = "Cobra la sesión o la venta actual.",
        ["Cobrar y cerrar"] = "Cobra la sesión y libera el equipo.",
        ["Configuración / token"] = "Muestra la configuración y el token del equipo.",
        ["Confirmar"] = "Confirma la operación.",
        ["Consultar"] = "Consulta los datos seleccionados.",
        ["Consultar BCV ahora"] = "Descarga la tasa oficial del BCV.",
        ["Control de volumen"] = "Abre el control de volumen del equipo.",
        ["Copiar"] = "Copia el texto al portapapeles.",
        ["Copiar token"] = "Copia el token de vinculación del equipo.",
        ["Crear respaldo ahora"] = "Crea una copia de seguridad de la base de datos.",
        ["Desactivar"] = "Desactiva el registro seleccionado.",
        ["Desbloquear entrada"] = "Devuelve el control del teclado y mouse.",
        ["Desinstalar CyberManager"] = "Quita la aplicación y sus datos de este equipo.",
        ["Editar"] = "Abre el registro seleccionado para editarlo.",
        ["Eliminar"] = "Elimina el registro seleccionado.",
        ["Enviar mensaje"] = "Muestra un mensaje en el equipo.",
        ["Encender monitor"] = "Enciende el monitor del equipo.",
        ["Enviar pedido a la estación"] = "Envía el pedido al administrador.",
        ["Entrar a la consola"] = "Inicia sesión en la consola.",
        ["Exportar copia"] = "Guarda una copia de la base de datos en un archivo.",
        ["Exportar CSV"] = "Exporta los datos a un archivo CSV.",
        ["Extender tiempo"] = "Añade tiempo a la sesión activa.",
        ["Guardar ahora"] = "Guarda los cambios pendientes.",
        ["Guardar cambios"] = "Guarda los cambios del formulario.",
        ["Guardar y aplicar"] = "Guarda y aplica la configuración.",
        ["Guardar y continuar"] = "Guarda la configuración y abre el equipo.",
        ["Historial"] = "Muestra el historial del registro.",
        ["Imprimir ticket"] = "Imprime un ticket de la venta actual.",
        ["Iniciar sesión"] = "Inicia una sesión en el equipo.",
        ["Limpiar"] = "Limpia la búsqueda o el filtro.",
        ["Mantenimiento"] = "Pone el equipo en mantenimiento.",
        ["Mensaje"] = "Envía un mensaje al equipo.",
        ["Mi cuenta"] = "Muestra el consumo de la sesión.",
        ["Movimiento"] = "Registra un ingreso o egreso de caja.",
        ["Movimientos"] = "Muestra los movimientos de caja.",
        ["Nueva categoría"] = "Crea una categoría de productos.",
        ["Nueva reserva"] = "Crea una reserva de equipo.",
        ["Nueva tarifa"] = "Crea una tarifa por tiempo.",
        ["Nueva zona"] = "Crea una zona del cyber.",
        ["Nuevo"] = "Crea un registro nuevo.",
        ["Nuevo cliente"] = "Registra un cliente nuevo.",
        ["Nuevo equipo"] = "Registra un equipo nuevo.",
        ["Nuevo paquete"] = "Crea un paquete de tiempo.",
        ["Nuevo producto"] = "Registra un producto nuevo.",
        ["Nuevo usuario"] = "Crea un usuario de la consola.",
        ["Pausar tiempo"] = "Pausa el tiempo de la sesión.",
        ["Pedir productos"] = "Abre el pedido de productos.",
        ["Poner en mantenimiento"] = "Marca el equipo en mantenimiento.",
        ["Probar conexión"] = "Comprueba la conexión con el servidor.",
        ["Quitar"] = "Quita el elemento de la lista.",
        ["Quitar mantenimiento"] = "Devuelve el equipo a servicio.",
        ["Reanudar tiempo"] = "Reanuda el tiempo de la sesión.",
        ["Recargar"] = "Recarga la información de la pantalla.",
        ["Recargar saldo"] = "Añade saldo a la cuenta del cliente.",
        ["Recordar luego"] = "Oculta el aviso durante 2 horas.",
        ["Reenviar a equipos"] = "Vuelve a enviar la orden a los equipos.",
        ["Reenviar a terminales"] = "Vuelve a enviar la orden a las terminales.",
        ["Regenerar token"] = "Genera un token nuevo para el equipo.",
        ["Registrar compra"] = "Registra la compra de productos.",
        ["Registrar movimiento"] = "Registra un ingreso o egreso de caja.",
        ["Registrar tasa"] = "Guarda una nueva tasa de cambio.",
        ["Reiniciar equipo"] = "Reinicia el equipo.",
        ["Restablecer"] = "Restaura los valores predeterminados.",
        ["Restaurar desde archivo"] = "Restaura la base de datos desde un archivo.",
        ["Restaurar seleccionado"] = "Restaura la copia de seguridad seleccionada.",
        ["Silenciar"] = "Silencia el audio del equipo.",
        ["Subir"] = "Mueve el elemento una posición hacia arriba.",
        ["Subir imágenes"] = "Agrega imágenes a la galería.",
        ["Subir videos"] = "Agrega videos a las promociones.",
        ["Transferir a otro PC"] = "Cambia la sesión a otro equipo.",
        ["Vaciar"] = "Quita todos los productos del pedido.",
        ["Ver kardex del producto seleccionado"] = "Muestra los movimientos del producto.",
        ["Ver novedades"] = "Abre la página de la nueva versión.",
        ["Ver pantalla"] = "Abre la pantalla remota del equipo.",
        ["Ver pantalla remota"] = "Muestra la pantalla del equipo.",
        ["Ver todo el kardex"] = "Muestra los movimientos de todos los productos.",
        ["Vista previa"] = "Muestra una vista previa del contenido."
    };

    /// <summary>Devuelve la descripción del botón traducida al idioma actual.</summary>
    public static string? Tip(string? content)
        => content is not null && ButtonTips.TryGetValue(content, out var tip) ? L.T(tip) : null;

    /// <summary>Asigna a un botón la descripción asociada a su texto y el retardo de 2 segundos.</summary>
    public static T WithTip<T>(T element, string? content) where T : FrameworkElement
    {
        if (Tip(content) is string tip)
        {
            element.ToolTip = tip;
            ToolTipService.SetInitialShowDelay(element, 2000);
            ToolTipService.SetShowDuration(element, 15000);
        }

        return element;
    }

    public static TextBlock Heading(string text, string style = "H1")
        => new() { Text = L.T(text), Style = Style(style) };

    public static TextBlock Muted(string text, string? style = "Muted")
        => new() { Text = L.T(text), Style = style is null ? null : Style(style) };

    public static TextBlock Label(string text) => new() { Text = L.T(text), Style = Style("Label") };

    public static TextBox Input(string? text = null, string? placeholder = null)
    {
        var box = new TextBox { Text = text ?? string.Empty };
        if (placeholder is not null) box.ToolTip = L.T(placeholder);
        return box;
    }

    public static TextBox Multiline(string? text = null, int height = 80)
        => new()
        {
            Text = text ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = height,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

    public static ComboBox Combo<T>(IEnumerable<Option<T>> options, int selectedIndex = 0)
    {
        var list = options.ToList();
        var combo = new ComboBox
        {
            ItemsSource = list,
            DisplayMemberPath = nameof(Option<object>.DisplayText),
            SelectedIndex = list.Count > 0 ? Math.Clamp(selectedIndex, 0, list.Count - 1) : -1
        };
        return combo;
    }

    public static Button Button(string content, string style, Action click, double? width = null)
    {
        var button = new Button { Content = L.T(content), Style = Style(style) };
        if (width is double w) button.Width = w;
        button.Click += (_, _) => click();
        return WithTip(button, content);
    }

    public static Button SmallButton(string content, string style, Action click)
    {
        var button = Button(content, style, click);
        button.Padding = new Thickness(10, 5, 10, 5);
        button.FontSize = 12;
        return button;
    }

    public static StackPanel Stack(Orientation orientation, double gap, params FrameworkElement[] children)
    {
        var panel = new StackPanel { Orientation = orientation };

        for (var i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (i > 0)
            {
                var margin = child.Margin;
                child.Margin = orientation == Orientation.Horizontal
                    ? new Thickness(margin.Left + gap, margin.Top, margin.Right, margin.Bottom)
                    : new Thickness(margin.Left, margin.Top + gap, margin.Right, margin.Bottom);
            }
            panel.Children.Add(child);
        }

        return panel;
    }

    public static StackPanel VStack(params FrameworkElement[] children) => Stack(Orientation.Vertical, 10, children);
    public static StackPanel VStack(double gap, params FrameworkElement[] children) => Stack(Orientation.Vertical, gap, children);
    public static StackPanel HStack(params FrameworkElement[] children) => Stack(Orientation.Horizontal, 8, children);
    public static StackPanel HStack(double gap, params FrameworkElement[] children) => Stack(Orientation.Horizontal, gap, children);

    public static Border Card(UIElement child, double padding = 16, Thickness? margin = null)
        => new()
        {
            Style = Style("Card"),
            Padding = new Thickness(padding),
            Margin = margin ?? new Thickness(0),
            Child = child
        };

    public static Border Panel(UIElement child, double padding = 12)
        => new() { Style = Style("Panel"), Padding = new Thickness(padding), Child = child };

    public static StackPanel Field(string label, FrameworkElement control)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        panel.Children.Add(Label(L.T(label)));
        panel.Children.Add(control);
        return panel;
    }

    public static Grid PageHeader(string title, string subtitle, params FrameworkElement[] actions)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var texts = new StackPanel();
        texts.Children.Add(Heading(title));
        texts.Children.Add(new TextBlock
        {
            Text = L.T(subtitle),
            Style = Style("Muted"),
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        grid.Children.Add(texts);

        if (actions.Length > 0)
        {
            var buttons = HStack(8, actions);
            buttons.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(buttons, 1);
            grid.Children.Add(buttons);
        }

        return grid;
    }

    public static TextBlock Section(string title)
        => new()
        {
            Text = L.T(title),
            Style = Style("H2"),
            Margin = new Thickness(0, 0, 0, 10)
        };

    public static Border StatCard(string label, string value, string? hint = null, string accent = "AccentBrush")
        => StatCard(label, value, hint, accent, out _);

    public static Border StatCard(string label, string value, string? hint, string accent, out TextBlock valueText)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(new Border
        {
            Background = Brush(accent),
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 2, 0, 2)
        });

        var texts = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        texts.Children.Add(new TextBlock
        {
            Text = L.T(label).ToUpperInvariant(),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("MutedBrush")
        });

        valueText = new TextBlock
        {
            Text = value,
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 4, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        texts.Children.Add(valueText);

        if (!string.IsNullOrWhiteSpace(hint))
        {
            texts.Children.Add(new TextBlock
            {
                Text = L.T(hint),
                Style = Style("Muted"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        return new Border { Style = Style("Card"), Padding = new Thickness(14), Child = grid };
    }

    public static DataGridTextColumn Col(string header, string path, double width = 0, string? format = null)
    {
        var column = new DataGridTextColumn { Header = L.T(header), Binding = new Binding(path) };
        if (format is not null) column.Binding.StringFormat = format;
        if (width > 0) column.Width = new DataGridLength(width);
        return column;
    }

    public static DataGridTemplateColumn TemplateCol(string header, FrameworkElementFactory factory, double width = 0)
    {
        var column = new DataGridTemplateColumn
        {
            Header = L.T(header),
            CellTemplate = new DataTemplate { VisualTree = factory }
        };
        if (width > 0) column.Width = new DataGridLength(width);
        return column;
    }

    public static FrameworkElementFactory Text(string path, string? brushPath = null,
        bool bold = false, string? format = null, TextAlignment align = TextAlignment.Left)
    {
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetValue(TextBlock.TextAlignmentProperty, align);
        factory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        if (bold) factory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);

        var binding = new Binding(path);
        if (format is not null) binding.StringFormat = format;
        factory.SetBinding(TextBlock.TextProperty, binding);

        if (brushPath is not null)
            factory.SetBinding(TextBlock.ForegroundProperty, new Binding(brushPath));

        return factory;
    }

    public static FrameworkElementFactory Pill(string textPath, string brushPath)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.PaddingProperty, new Thickness(8, 2, 8, 2));
        border.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        border.SetBinding(Border.BackgroundProperty, new Binding(brushPath) { Converter = new PillBackgroundConverter() });

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(TextBlock.FontSizeProperty, 11.0);
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        text.SetBinding(TextBlock.TextProperty, new Binding(textPath));
        text.SetBinding(TextBlock.ForegroundProperty, new Binding(brushPath));

        border.AppendChild(text);
        return border;
    }

    public static DataGridTemplateColumn ActionCol(string header, string text, RoutedEventHandler click,
        string style = "GhostButton", double width = 110)
    {
        var factory = new FrameworkElementFactory(typeof(Button));
        factory.SetValue(ContentControl.ContentProperty, L.T(text));
        factory.SetValue(FrameworkElement.StyleProperty, Style(style));
        factory.SetValue(Control.PaddingProperty, new Thickness(9, 3, 9, 3));
        factory.SetValue(Control.FontSizeProperty, 11.0);
        factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        factory.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 4, 6, 4));
        if (Tip(text) is string tip) factory.SetValue(FrameworkElement.ToolTipProperty, tip);
        factory.SetValue(ToolTipService.InitialShowDelayProperty, 2000);
        factory.AddHandler(System.Windows.Controls.Button.ClickEvent, click);

        var column = new DataGridTemplateColumn
        {
            Header = L.T(header),
            CellTemplate = new DataTemplate { VisualTree = factory }
        };
        if (width > 0) column.Width = new DataGridLength(width);
        return column;
    }

    public static DataGrid Table(params DataGridColumn[] columns)
    {
        var grid = new DataGrid();
        foreach (var column in columns) grid.Columns.Add(column);
        return grid;
    }

    public static ScrollViewer Scroll(UIElement child)
        => new() { Content = child, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
}

/// <summary>Convierte un color hexadecimal en un fondo translúcido para las etiquetas de estado.</summary>
public sealed class PillBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex)) return Brushes.Transparent;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(Color.FromArgb(38, color.R, color.G, color.B));
        }
        catch
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => Binding.DoNothing;
}

