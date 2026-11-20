using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CyberManager.Shared.Dtos;

namespace CyberManager.Admin.Views;

/// <summary>
/// Panel de diagnóstico: muestra los hallazgos de reglas del servidor y la explicación
/// opcional generada por un proveedor de IA compatible con OpenAI.
/// </summary>
public sealed class TroubleshootView : UserControl, IRefreshable
{
    private readonly TextBlock _summaryText = new()
    {
        Text = "Ejecute un diagnóstico para revisar el estado del sistema.",
        Style = Kit.Style("Muted"),
        TextWrapping = TextWrapping.Wrap
    };

    private readonly UniformGrid _stats = new() { Columns = 4, Margin = new Thickness(0, 0, 0, 14) };
    private readonly StackPanel _findings = new();
    private readonly TextBlock _aiState = new() { Style = Kit.Style("Muted"), TextWrapping = TextWrapping.Wrap };

    private readonly TextBox _aiText = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        FontFamily = new FontFamily("Consolas"),
        FontSize = 12,
        MinHeight = 130,
        MaxHeight = 260,
        Padding = new Thickness(10),
        Margin = new Thickness(0, 8, 0, 0)
    };

    private readonly TextBox _logsText = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontFamily = new FontFamily("Consolas"),
        FontSize = 11,
        Height = 150,
        Padding = new Thickness(10),
        Margin = new Thickness(0, 8, 0, 0)
    };

    private TroubleshootReportDto? _report;

    public TroubleshootView()
    {
        var root = new StackPanel();
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(18),
            Content = root
        };
        Content = scroll;

        root.Children.Add(Kit.PageHeader("Solucionador de problemas",
            "Revisa equipos, versiones y registros. La IA en la nube solo explica: no ejecuta acciones.",
            Kit.Button("Analizar con IA", "PrimaryButton",
                () => _ = Ui.Run(() => RunAsync(true), "No se pudo ejecutar el diagnóstico")),
            Kit.Button("Solo reglas", "GhostButton",
                () => _ = Ui.Run(() => RunAsync(false), "No se pudo ejecutar el diagnóstico"))));

        root.Children.Add(_stats);

        root.Children.Add(Card(new TextBlock
        {
            Text = "RESUMEN",
            Style = Kit.Style("Label"),
            Margin = new Thickness(0, 0, 0, 6)
        }, _summaryText));

        var findingsStack = new StackPanel();
        findingsStack.Children.Add(Kit.Section("Hallazgos"));
        findingsStack.Children.Add(_findings);
        root.Children.Add(Card(findingsStack));

        var aiStack = new StackPanel();
        aiStack.Children.Add(Kit.Section("Análisis de IA en la nube"));
        aiStack.Children.Add(_aiState);
        aiStack.Children.Add(_aiText);
        root.Children.Add(Card(aiStack));

        var logStack = new StackPanel();
        logStack.Children.Add(Kit.Section("Registros recientes (sanitizados)"));
        logStack.Children.Add(_logsText);
        root.Children.Add(Card(logStack));

        Render();
    }

    public async Task RefreshAsync()
    {
        if (_report is null) await RunAsync(false);
        else Render();
    }

    private async Task RunAsync(bool useAi)
    {
        _summaryText.Text = "Analizando el estado del servidor y los equipos…";
        var report = await App.Api.DiagnoseAsync(new TroubleshootRequestDto(
            IncludeLogs: true,
            UseAi: useAi));
        _report = report;
        Render();
    }

    private void Render()
    {
        _stats.Children.Clear();
        _findings.Children.Clear();

        if (_report is null)
        {
            _summaryText.Text = "Ejecute un diagnóstico para revisar el estado del sistema.";
            _aiState.Text = "La explicación de IA aparecerá después de analizar.";
            _aiText.Text = string.Empty;
            _logsText.Text = string.Empty;
            return;
        }

        var critical = _report.Findings.Count(f => f.Severity == TroubleshootSeverity.Critical);
        var warnings = _report.Findings.Count(f => f.Severity == TroubleshootSeverity.Warning);

        _stats.Children.Add(Kit.StatCard("Servidor", _report.ServerVersion, "Versión instalada"));
        _stats.Children.Add(Kit.StatCard("Equipos",
            $"{_report.OnlineStations}/{_report.Stations}",
            "En línea / activos",
            _report.OnlineStations == _report.Stations ? "SuccessBrush" : "WarningBrush"));
        _stats.Children.Add(Kit.StatCard("Hallazgos",
            _report.Findings.Count.ToString(),
            _report.Summary,
            critical > 0 ? "DangerBrush" : warnings > 0 ? "WarningBrush" : "AccentBrush"));
        _stats.Children.Add(Kit.StatCard("IA nube",
            _report.AiUsed ? "Usada" : _report.AiConfigured ? "Lista" : "Sin clave",
            _report.AiError ?? "Explicación opcional",
            _report.AiUsed ? "SuccessBrush" : "MutedBrush"));

        _summaryText.Text = _report.Summary + $" Generado {_report.GeneratedUtc.ToLocalTime():g}.";

        if (_report.Findings.Count == 0)
        {
            _findings.Children.Add(new TextBlock
            {
                Text = "No se detectaron incidencias con la información disponible.",
                Style = Kit.Style("Muted")
            });
        }
        else
        {
            foreach (var finding in _report.Findings.OrderByDescending(f => f.Severity))
                _findings.Children.Add(FindingCard(finding));
        }

        _aiState.Text = _report.AiUsed
            ? "Explicación generada por el proveedor configurado. Las acciones siguen requiriendo confirmación en la consola."
            : _report.AiError is not null
                ? "IA no disponible: " + _report.AiError
                : "El análisis se ejecutó solo con reglas.";
        _aiText.Text = _report.AiExplanation;

        _logsText.Text = _report.LogExcerpts.Count > 0
            ? string.Join(Environment.NewLine, _report.LogExcerpts)
            : "No había errores, advertencias ni líneas recientes para mostrar.";
    }

    private static Border FindingCard(TroubleshootFindingDto finding)
    {
        var (icon, brushName) = finding.Severity switch
        {
            TroubleshootSeverity.Critical => ("!", "DangerBrush"),
            TroubleshootSeverity.Warning => ("△", "WarningBrush"),
            _ => ("i", "AccentBrush")
        };
        var brush = Kit.Brush(brushName);

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = $"{icon}  {finding.Title}",
            FontWeight = FontWeights.SemiBold,
            Foreground = brush,
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock
        {
            Text = finding.Detail,
            Style = Kit.Style("Muted"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Acción sugerida: " + finding.Recommendation,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Kit.Brush("TextBrush"),
            Margin = new Thickness(0, 6, 0, 0)
        });

        return new Border
        {
            Style = Kit.Style("Panel"),
            BorderBrush = brush,
            BorderThickness = new Thickness(3, 1, 1, 1),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10),
            Child = stack
        };
    }

    private static Border Card(params UIElement[] children)
    {
        var stack = new StackPanel();
        foreach (var child in children) stack.Children.Add(child);
        return new Border
        {
            Style = Kit.Style("Card"),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 14),
            Child = stack
        };
    }
}
