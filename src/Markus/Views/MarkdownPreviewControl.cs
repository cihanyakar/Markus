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
        var document = MarkdownPipeline.Parse(source);
        foreach (var control in MarkdownRenderer.Render(document))
        {
            _container.Children.Add(control);
        }
    }
}
