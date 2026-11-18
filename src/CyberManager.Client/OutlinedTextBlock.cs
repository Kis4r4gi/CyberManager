using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace CyberManager.Client;

/// <summary>
/// Texto con contorno. WPF no dibuja el borde de un <see cref="System.Windows.Controls.TextBlock"/>,
/// así que se construye la geometría de los glifos y se pinta primero el trazo y luego el relleno.
/// Se usa en la pantalla de bloqueo para que el texto se lea sobre fondos claros.
/// </summary>
public sealed class OutlinedTextBlock : FrameworkElement
{
    // Ancho grande para que FormattedText no parta la línea cuando no se pide ajuste.
    private const double NoWrapWidth = 100000;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(string.Empty,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(Brushes.White,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(Brushes.Black,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(2.5,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(
        typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(new FontFamily("Segoe UI"),
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(
        typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(12.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(
        typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(FontWeights.Normal,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontStyleProperty = TextElement.FontStyleProperty.AddOwner(
        typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(FontStyles.Normal,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextAlignmentProperty = TextBlock.TextAlignmentProperty.AddOwner(
        typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(TextAlignment.Left,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextWrappingProperty = TextBlock.TextWrappingProperty.AddOwner(
        typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(TextWrapping.NoWrap,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public FontStyle FontStyle
    {
        get => (FontStyle)GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    public TextAlignment TextAlignment
    {
        get => (TextAlignment)GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => (TextWrapping)GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var wrap = TextWrapping != TextWrapping.NoWrap;
        if (!wrap)
        {
            var single = CreateFormattedText(NoWrapWidth);
            return new Size(single.Width + StrokeThickness, single.Height + StrokeThickness);
        }

        var limit = double.IsInfinity(availableSize.Width) ? NoWrapWidth : availableSize.Width;
        if (!double.IsNaN(MaxWidth) && MaxWidth < limit) limit = MaxWidth;

        // El contorno ocupa parte del ancho; el texto se ajusta al resto.
        var wrapWidth = Math.Max(1, limit - StrokeThickness);
        var wrapped = CreateFormattedText(wrapWidth);
        return new Size(wrapWidth + StrokeThickness, wrapped.Height + StrokeThickness);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Text)) return;

        var wrap = TextWrapping != TextWrapping.NoWrap;
        var maxWidth = wrap
            ? Math.Max(1, ActualWidth - StrokeThickness)
            : NoWrapWidth;

        var formatted = CreateFormattedText(maxWidth);
        var origin = new Point(StrokeThickness / 2, StrokeThickness / 2);
        var geometry = formatted.BuildGeometry(origin);

        if (Stroke is not null && StrokeThickness > 0)
            dc.DrawGeometry(null, new Pen(Stroke, StrokeThickness), geometry);

        if (Fill is not null)
            dc.DrawGeometry(Fill, null, geometry);
    }

    protected override AutomationPeer OnCreateAutomationPeer()
        => new OutlinedTextBlockAutomationPeer(this);

    private FormattedText CreateFormattedText(double maxWidth)
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretches.Normal);
        var text = new FormattedText(
            Text ?? string.Empty,
            CultureInfo.CurrentUICulture,
            FlowDirection,
            typeface,
            FontSize,
            Fill ?? Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, maxWidth)
        };

        // Sin ajuste la línea no se parte; el ancho del control es el del texto.
        text.TextAlignment = TextWrapping == TextWrapping.NoWrap ? TextAlignment.Left : TextAlignment;
        return text;
    }

    private sealed class OutlinedTextBlockAutomationPeer(OutlinedTextBlock owner)
        : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetNameCore() => ((OutlinedTextBlock)Owner).Text;

        protected override AutomationControlType GetAutomationControlTypeCore()
            => AutomationControlType.Text;

        protected override string GetClassNameCore() => nameof(OutlinedTextBlock);
    }
}
