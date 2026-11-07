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
        return button;
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

