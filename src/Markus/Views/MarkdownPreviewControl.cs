using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Markus.Rendering;
using Markus.Services;

namespace Markus.Views;

internal sealed class MarkdownPreviewControl : UserControl
{
    public static readonly StyledProperty<string?> SourceProperty = AvaloniaProperty.Register<
        MarkdownPreviewControl,
        string?
    >(nameof(Source));

    private readonly StackPanel _container;
    private readonly Dictionary<int, Control> _lineToControl = new Dictionary<int, Control>();

    public MarkdownPreviewControl()
    {
        _container = new StackPanel
        {
            Spacing = 0,
            MaxWidth = 820,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var scroll = new ScrollViewer
        {
            Padding = new Thickness(32, 24),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _container,
        };

        Content = scroll;
    }

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool ScrollToLine(int sourceLine)
    {
        if (_lineToControl.TryGetValue(sourceLine, out var control))
        {
            control.BringIntoView();
            return true;
        }
        return false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            Render(change.GetNewValue<string?>() ?? string.Empty);
        }
    }

    private void Render(string source)
    {
        _container.Children.Clear();
        _lineToControl.Clear();
        var document = MarkdownPipeline.Parse(source);
        foreach (var rendered in MarkdownRenderer.Render(document))
        {
            _container.Children.Add(rendered.Control);
            _lineToControl[rendered.SourceLine] = rendered.Control;
        }
    }
}
