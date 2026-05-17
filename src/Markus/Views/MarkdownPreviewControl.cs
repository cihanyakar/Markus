using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Markus.Rendering;
using Markus.Services;

namespace Markus.Views;

// _renderCts is disposed in DetachedFromVisualTree, which Avalonia raises on
// teardown. Wrapping the UserControl in IDisposable just to satisfy CA1001
// would conflict with Avalonia's own lifecycle.
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "_renderCts is disposed in DetachedFromVisualTree, matching Avalonia's lifecycle."
)]
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

    // Cold-start gating: render the first N blocks eagerly (no UI yield) so
    // the user sees content within the first frame, even on a multi-MB doc.
    // After that, switch to a frame-budget strategy: keep adding controls
    // until we've burned <FrameBudgetMs>, then release the UI thread.
    private const int EagerFirstPaintCount = 30;
    private const long FrameBudgetMs = 8;

    private readonly StackPanel _container;
    private readonly Dictionary<int, Control> _lineToControl = new Dictionary<int, Control>();
    private readonly PreviewSearcher _searcher = new PreviewSearcher();
    private readonly DispatcherTimer _debounceTimer;
    private System.Threading.CancellationTokenSource? _renderCts;
    private string _pendingSource = string.Empty;

    public MarkdownPreviewControl()
    {
        // StackPanel stretches to the available width (default for vertical
        // panels) so content fills the split pane. We used to cap at 820pt
        // with HorizontalAlignment=Left, which left dead space on the right
        // in wide panes and pushed wrap calculations off the visible viewport.
        _container = new StackPanel { Spacing = 0 };

        // Right padding clears the vertical scrollbar's overlay zone so wrap
        // and horizontal-scroll endings never tuck under it. Set the padding
        // on a content wrapper inside the ScrollViewer so the scrollbar still
        // lives at the viewport's right edge but content stops short of it.
        var wrapper = new Border { Padding = new Thickness(20, 18, 32, 18), Child = _container };
        Scroll = new ScrollViewer
        {
            Padding = new Thickness(0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            AllowAutoHide = false,
            Content = wrapper,
        };

        Content = Scroll;
        Focusable = true;
        // Children (SelectableTextBlocks) consume click events for selection,
        // so the bubbled pointerpressed never reaches us. Listen on the tunnel
        // so a click anywhere in the preview surface focuses this control,
        // letting Cmd+F target the preview.
        AddHandler(PointerPressedEvent, (_, _) => Focus(), Avalonia.Interactivity.RoutingStrategies.Tunnel);

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _debounceTimer.Tick += OnDebounceElapsed;
        DetachedFromVisualTree += (_, _) =>
        {
            _renderCts?.Dispose();
            _renderCts = null;
        };
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

    public ScrollViewer Scroll { get; }

    public bool ScrollToLine(int sourceLine)
    {
        if (_lineToControl.TryGetValue(sourceLine, out var control))
        {
            control.BringIntoView();
            return true;
        }
        // Caret may land on a line without a top-level block (a blank line,
        // an inline-only line, etc.). Walk back to the nearest block whose
        // source line is <= sourceLine so sync-scroll lands somewhere sane.
        var bestLine = -1;
        Control? best = null;
        foreach (var pair in _lineToControl)
        {
            if (pair.Key > sourceLine || pair.Key <= bestLine)
            {
                continue;
            }
            bestLine = pair.Key;
            best = pair.Value;
        }
        if (best is null)
        {
            return false;
        }
        best.BringIntoView();
        return true;
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
            ScheduleRender(change.GetNewValue<string?>() ?? string.Empty);
            return;
        }
        if (change.Property == SearchTermProperty || change.Property == SearchCaseSensitiveProperty)
        {
            ScheduleRender(Source ?? string.Empty);
        }
    }

    private static async System.Threading.Tasks.Task<Markdig.Syntax.MarkdownDocument?> ParseAsync(
        string source,
        System.Threading.CancellationToken token
    )
    {
        try
        {
            return await System.Threading.Tasks.Task.Run(() => MarkdownPipeline.Parse(source), token);
        }
        catch (OperationCanceledException)
        {
            return null;
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

    private void ScheduleRender(string source)
    {
        // Coalesce rapid SourceText updates (typing in the editor) into one
        // render after a brief idle. Cancellation handles the case where a
        // new render fires before the previous chunked loop finishes.
        _pendingSource = source;
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void OnDebounceElapsed(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        _ = RenderAsync(_pendingSource);
    }

    private async System.Threading.Tasks.Task RenderAsync(string source)
    {
        var token = await BeginRenderAsync();
        if (token.IsCancellationRequested)
        {
            return;
        }
        var document = await ParseAsync(source, token);
        if (document is null || token.IsCancellationRequested)
        {
            return;
        }
        await StreamBlocksAsync(document, token);
        ApplySearch();
    }

    private async System.Threading.Tasks.Task<System.Threading.CancellationToken> BeginRenderAsync()
    {
        if (_renderCts is { } previous)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }
        _renderCts = new System.Threading.CancellationTokenSource();

        _container.Children.Clear();
        _lineToControl.Clear();

        var theme = MarkdownRenderer.Theme;
        Background = new SolidColorBrush(theme.Background);

        // Wrap mode flips the ScrollViewer's horizontal scrollbar off so the
        // Auto policy doesn't reserve ghost width during measure.
        Scroll.HorizontalScrollBarVisibility = MarkdownRenderer.WrapCode
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Auto;

        return _renderCts.Token;
    }

    private async System.Threading.Tasks.Task StreamBlocksAsync(
        Markdig.Syntax.MarkdownDocument document,
        System.Threading.CancellationToken token
    )
    {
        _lineToControl.EnsureCapacity(document.Count + 16);
        var rendered = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var block in MarkdownRenderer.Render(document))
        {
            if (token.IsCancellationRequested)
            {
                return;
            }
            _container.Children.Add(block.Control);
            _lineToControl[block.SourceLine] = block.Control;
            rendered++;
            if (rendered < EagerFirstPaintCount)
            {
                continue;
            }
            if (sw.ElapsedMilliseconds < FrameBudgetMs)
            {
                continue;
            }
            await System.Threading.Tasks.Task.Yield();
            sw.Restart();
        }
    }
}
