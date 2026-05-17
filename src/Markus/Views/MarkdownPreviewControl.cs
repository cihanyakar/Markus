using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Markus.Rendering;
using Markus.Services;

namespace Markus.Views;

internal sealed class MarkdownPreviewControl : UserControl
{
    public static readonly StyledProperty<string?> SourceProperty = AvaloniaProperty.Register<
        MarkdownPreviewControl,
        string?
    >(nameof(Source));

    public static readonly StyledProperty<string?> SearchTermProperty = AvaloniaProperty.Register<
        MarkdownPreviewControl,
        string?
    >(nameof(SearchTerm));

    public static readonly StyledProperty<bool> SearchCaseSensitiveProperty = AvaloniaProperty.Register<
        MarkdownPreviewControl,
        bool
    >(nameof(SearchCaseSensitive));

    private readonly StackPanel _container;
    private readonly Dictionary<int, Control> _lineToControl = new Dictionary<int, Control>();
    private readonly PreviewSearcher _searcher = new PreviewSearcher();

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
        Focusable = true;
        // Children (SelectableTextBlocks) consume click events for selection,
        // so the bubbled pointerpressed never reaches us. Listen on the tunnel
        // so a click anywhere in the preview surface focuses this control,
        // letting Cmd+F target the preview.
        AddHandler(PointerPressedEvent, (_, _) => Focus(), Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public string? SearchTerm
    {
        get => GetValue(SearchTermProperty);
        set => SetValue(SearchTermProperty, value);
    }

    public bool SearchCaseSensitive
    {
        get => GetValue(SearchCaseSensitiveProperty);
        set => SetValue(SearchCaseSensitiveProperty, value);
    }

    public int MatchCount => _searcher.MatchCount;

    public int ActiveMatchIndex => _searcher.ActiveIndex;

    public bool ScrollToLine(int sourceLine)
    {
        if (_lineToControl.TryGetValue(sourceLine, out var control))
        {
            control.BringIntoView();
            return true;
        }
        return false;
    }

    public void MoveToNextMatch()
    {
        if (_searcher.MoveNext())
        {
            PreviewSearcher.FindControlParent(_searcher.ActiveMatch?.Parent)?.BringIntoView();
        }
    }

    public void MoveToPrevMatch()
    {
        if (_searcher.MovePrev())
        {
            PreviewSearcher.FindControlParent(_searcher.ActiveMatch?.Parent)?.BringIntoView();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            Render(change.GetNewValue<string?>() ?? string.Empty);
            ApplySearch();
            return;
        }
        if (change.Property == SearchTermProperty || change.Property == SearchCaseSensitiveProperty)
        {
            Render(Source ?? string.Empty);
            ApplySearch();
        }
    }

    private void ApplySearch()
    {
        _searcher.Highlight(_lineToControl.Values, SearchTerm ?? string.Empty, SearchCaseSensitive);
        if (_searcher.MatchCount > 0)
        {
            PreviewSearcher.FindControlParent(_searcher.ActiveMatch?.Parent)?.BringIntoView();
        }
    }

    private void Render(string source)
    {
        _container.Children.Clear();
        _lineToControl.Clear();
        var theme = MarkdownRenderer.Theme;
        Background = new SolidColorBrush(theme.Background);
        var document = MarkdownPipeline.Parse(source);
        foreach (var rendered in MarkdownRenderer.Render(document))
        {
            _container.Children.Add(rendered.Control);
            _lineToControl[rendered.SourceLine] = rendered.Control;
        }
    }
}
