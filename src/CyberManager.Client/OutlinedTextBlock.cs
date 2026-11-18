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

    // Pasadas del resplandor; más pasadas, transición más suave.
    private const int GlowSteps = 16;

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

    public static readonly DependencyProperty GlowProperty = DependencyProperty.Register(
        nameof(Glow), typeof(Brush), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(Brushes.Black,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlowRadiusProperty = DependencyProperty.Register(
        nameof(GlowRadius), typeof(double), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(6.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlowOpacityProperty = DependencyProperty.Register(
        nameof(GlowOpacity), typeof(double), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(0.9,
            FrameworkPropertyMetadataOptions.AffectsRender));

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

    public Brush Glow
    {
        get => (Brush)GetValue(GlowProperty);
        set => SetValue(GlowProperty, value);
    }

    public double GlowRadius
    {
        get => (double)GetValue(GlowRadiusProperty);
        set => SetValue(GlowRadiusProperty, value);
    }

    public double GlowOpacity
    {
        get => (double)GetValue(GlowOpacityProperty);
        set => SetValue(GlowOpacityProperty, value);
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
        // El trazo y el resplandor se dibujan fuera de la caja del texto.
        var margin = StrokeThickness + Math.Max(0, GlowRadius) * 2;
        var wrap = TextWrapping != TextWrapping.NoWrap;
        if (!wrap)
        {
            var single = CreateFormattedText(NoWrapWidth);
            return new Size(single.Width + margin, single.Height + margin);
        }

        var limit = double.IsInfinity(availableSize.Width) ? NoWrapWidth : availableSize.Width;
        if (!double.IsNaN(MaxWidth) && MaxWidth < limit) limit = MaxWidth;

        // El contorno y el resplandor ocupan parte del ancho; el texto se ajusta al resto.
        var wrapWidth = Math.Max(1, limit - margin);
        var wrapped = CreateFormattedText(wrapWidth);
        return new Size(wrapWidth + margin, wrapped.Height + margin);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Text)) return;

        var margin = StrokeThickness + Math.Max(0, GlowRadius) * 2;
        var wrap = TextWrapping != TextWrapping.NoWrap;
        var maxWidth = wrap
            ? Math.Max(1, ActualWidth - margin)
            : NoWrapWidth;

        var formatted = CreateFormattedText(maxWidth);
        var origin = new Point(margin / 2, margin / 2);
        var geometry = formatted.BuildGeometry(origin);

        // Resplandor hacia afuera: varias pasadas de trazo cada vez más finas y opacas.
        if (Glow is not null && GlowRadius > 0 && GlowOpacity > 0)
        {
            for (var i = GlowSteps; i >= 1; i--)
            {
                var t = (double)i / GlowSteps;
                var thickness = StrokeThickness + GlowRadius * 2 * t;
                var brush = CreateGlowBrush(t);
                if (brush is null) continue;
                dc.DrawGeometry(null, new Pen(brush, thickness), geometry);
            }
        }

        if (Stroke is not null && StrokeThickness > 0)
            dc.DrawGeometry(null, new Pen(Stroke, StrokeThickness), geometry);

        if (Fill is not null)
            dc.DrawGeometry(Fill, null, geometry);
    }

    /// <summary>Mezcla el color del resplandor con una opacidad que crece hacia el glifo.</summary>
    private Brush? CreateGlowBrush(double t)
    {
        if (Glow is not SolidColorBrush solid) return Glow;

        // La suma de las pasadas da un halo suave: opaco junto al glifo y desvanecido al borde.
        var alpha = (byte)Math.Clamp(255.0 * GlowOpacity * 2.0 * (1.0 - t) / GlowSteps, 0, 255);
        var brush = new SolidColorBrush(Color.FromArgb(alpha, solid.Color.R, solid.Color.G, solid.Color.B));
        brush.Freeze();
        return brush;
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
