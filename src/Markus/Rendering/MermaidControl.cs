using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Markus.Services;

namespace Markus.Rendering;

// Cancel-only teardown is intentional for _cts: an in-flight StartRenderAsync
// still reads ct.IsCancellationRequested off this source, and Microsoft docs
// say accessing any member of a disposed CTS is undefined. We null the field
// and let the GC reclaim the source once the render task releases its
// captured token. The class is therefore NOT IDisposable (CA1001 suppressed)
// because there is no caller-driven dispose path and an unreachable Dispose
// method would mislead future maintainers. Both suppressions are scoped to
// the _cts field's lifecycle; any future IDisposable field added here must
// reckon with these rules explicitly.
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Cancel-only teardown; see class doc."
)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Reliability",
    "S2930:IDisposables should be disposed",
    Justification = "Cancel-only teardown; see class doc."
)]
internal sealed class MermaidControl : ContentControl
{
    private readonly string _source;
    private CancellationTokenSource? _cts;
    private string? _tempSvgPath;

    public MermaidControl(string mermaidSource)
    {
        _source = mermaidSource;
        HorizontalAlignment = HorizontalAlignment.Center;
        Margin = new Thickness(0, 6, 0, 10);
        Content = BuildLoadingState();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = StartRenderAsync(MarkdownRenderer.MermaidScale, _cts.Token);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _cts?.Cancel();
        _cts = null;
        DeleteTempFile();
        base.OnDetachedFromVisualTree(e);
    }

    private static Control BuildLoadingState()
    {
        return new TextBlock
        {
            Text = "Rendering diagram...",
            FontSize = 13,
            FontStyle = FontStyle.Italic,
            Foreground = new SolidColorBrush(Color.FromArgb(120, 128, 128, 128)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12),
        };
    }

    private Control BuildSvgDiagram(string svgText, double userScale)
    {
        DeleteTempFile();
        var tempPath = Path.Combine(Path.GetTempPath(), $"markus_mermaid_{Path.GetRandomFileName()}.svg");
        File.WriteAllText(tempPath, svgText);
        _tempSvgPath = tempPath;

        var svg = new Avalonia.Svg.Skia.Svg(new Uri("file://" + tempPath))
        {
            Path = tempPath,
            Stretch = Stretch.Uniform,
        };

        return new Viewbox
        {
            Child = svg,
            MaxWidth = 400 * userScale,
            MaxHeight = 300 * userScale,
            Stretch = Stretch.Uniform,
        };
    }

    private void DeleteTempFile()
    {
        if (_tempSvgPath is null)
        {
            return;
        }

        try
        {
            File.Delete(_tempSvgPath);
        }
        catch (IOException)
        {
            // Best-effort cleanup; the OS will reclaim temp files eventually.
        }

        _tempSvgPath = null;
    }

    private async Task StartRenderAsync(double userScale, CancellationToken ct)
    {
        try
        {
            var svg = await MermaidRenderer.RenderToSvgAsync(_source, ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Re-check on the UI thread: a detach (which deletes the temp
                // file and won't run again) may have happened after the guard
                // above, and building here would write a temp file that leaks.
                if (ct.IsCancellationRequested)
                {
                    return;
                }
                Content = svg is not null ? BuildSvgDiagram(svg, userScale) : BuildErrorState();
            });
        }
        catch (OperationCanceledException)
        {
            // Control was detached before render completed.
        }
    }

    private Control BuildErrorState()
    {
        var label = new TextBlock
        {
            Text = "MERMAID — render failed",
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x70)),
            Margin = new Thickness(0, 0, 0, 6),
        };

        var raw = new SelectableTextBlock
        {
            Text = _source.Trim(),
            FontFamily = MarkdownRenderer.MonoFamily,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6E, 0x84)),
            TextWrapping = TextWrapping.Wrap,
        };

        var panel = new StackPanel { Spacing = 0 };
        panel.Children.Add(label);
        panel.Children.Add(raw);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(20, 0xFF, 0x3D, 0x55)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 0xFF, 0x3D, 0x55)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12),
            Child = panel,
        };
    }
}
