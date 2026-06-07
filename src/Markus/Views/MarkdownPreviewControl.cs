using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Markus.Rendering;
using Markus.Services;

namespace Markus.Views;

// How a source change should be turned into a render: skipped while the panel
// is hidden, painted at once on the first real content, or coalesced behind the
// typing debounce.
internal enum PreviewRenderSchedule
{
    Defer,
    Immediate,
    Debounced,
}

// Timers are disposed in DetachedFromVisualTree, which Avalonia raises on
// teardown. Wrapping the UserControl in IDisposable just to satisfy CA1001
// would conflict with Avalonia's own lifecycle.
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable fields are released in DetachedFromVisualTree, matching Avalonia's lifecycle."
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

    // Idle debounce: coalesce rapid SourceText updates into one render after
    // typing pauses. Force interval: when typing is continuous and idle never
    // fires, repaint at most every <ForceMs> so the preview can't drift more
    // than that behind the editor.
    private const int DebounceMs = 120;
    private const int ForceMs = 500;

    private readonly StackPanel _panelA;
    private readonly StackPanel _panelB;
    private readonly Dictionary<int, Control> _lineMapA = new Dictionary<int, Control>();
    private readonly Dictionary<int, Control> _lineMapB = new Dictionary<int, Control>();
    private readonly Grid _bufferGrid;
    private readonly PreviewSearcher _searcher = new PreviewSearcher();
    private readonly DispatcherTimer _debounceTimer;
    private readonly DispatcherTimer _forceTimer;
    private string _pendingSource = string.Empty;
    private string _lastRenderedSource = string.Empty;

    // Set when a source change arrived while this panel was hidden (an inactive
    // view-mode copy). The deferred render runs when the panel becomes visible.
    private bool _renderDeferredWhileHidden;

    // Monotonically increasing counter bumped by InvalidateRender so that a
    // forced re-render (theme/font change, same text) is detected even when
    // _pendingSource == _lastRenderedSource.
    private int _renderGeneration;
    private int _lastRenderedGeneration;

    // Renders are serialized through this flag: a tick handler returns early
    // if a render is already in flight; the in-flight render's loop will
    // pick up _pendingSource if it changed during streaming. No cancellation
    // tokens to lose state through.
    private bool _renderBusy;

    // The visible panel; the pending (offscreen) panel is the other one.
    // Renders stream into pending, then a synchronous swap flips visibility
    // and aligns Scroll.Offset so the source-line at the top stays anchored.
    private bool _activeIsA = true;

    public MarkdownPreviewControl()
    {
        _panelA = new StackPanel { Spacing = 0 };
        _panelB = new StackPanel
        {
            Spacing = 0,
            Opacity = 0,
            IsHitTestVisible = false,
        };
        _bufferGrid = new Grid();
        _bufferGrid.Children.Add(_panelA);
        _bufferGrid.Children.Add(_panelB);

        // Cap the body to a comfortable reading measure and center it so a wide
        // window doesn't stretch paragraphs and code blocks edge-to-edge. The
        // side padding doubles as the minimum gutter once the window is
        // narrower than the column.
        _bufferGrid.MaxWidth = 1000;
        _bufferGrid.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        var wrapper = new Border { Padding = new Thickness(22, 14, 30, 18), Child = _bufferGrid };
        Scroll = new ScrollViewer
        {
            Padding = new Thickness(0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            AllowAutoHide = false,
            Content = wrapper,
        };

        Content = Scroll;
        // Keep the reading position fixed when the window regains focus. A
        // ScrollViewer otherwise scrolls the previously-focused inline back into
        // view on re-activation, which jumps the preview to a different spot.
        ScrollViewer.SetBringIntoViewOnFocusChange(Scroll, false);
        Styles.Add(BuildSelectableTextStyle());
        Focusable = true;
        AddHandler(PointerPressedEvent, OnSurfacePointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMs) };
        _debounceTimer.Tick += OnDebounceElapsed;
        _forceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ForceMs) };
        _forceTimer.Tick += OnForceElapsed;
        DetachedFromVisualTree += (_, _) =>
        {
            _debounceTimer.Stop();
            _forceTimer.Stop();
        };
        // A hidden view-mode copy defers its render; paint it when it becomes
        // the active view (its container is shown). EffectiveViewportChanged is
        // the public signal for that transition; the copy's own IsVisible stays
        // true inside a hidden container, so it can't be used here.
        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    public event EventHandler? RenderStarted;

    public event EventHandler? RenderCompleted;

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

    private StackPanel ActivePanel => _activeIsA ? _panelA : _panelB;

    private StackPanel PendingPanel => _activeIsA ? _panelB : _panelA;

    private Dictionary<int, Control> ActiveLineMap => _activeIsA ? _lineMapA : _lineMapB;

    private Dictionary<int, Control> PendingLineMap => _activeIsA ? _lineMapB : _lineMapA;

    public void InvalidateRender()
    {
        _renderGeneration++;
        ScheduleRender(Source ?? string.Empty);
    }

    public bool ScrollToLine(int sourceLine)
    {
        if (ActiveLineMap.TryGetValue(sourceLine, out var control))
        {
            control.BringIntoView();
            return true;
        }
        var nearest = FindNearestAtOrBefore(ActiveLineMap, sourceLine);
        if (nearest is null)
        {
            return false;
        }
        nearest.BringIntoView();
        return true;
    }

    /// <summary>
    /// Aligns the block whose source line is at or before <paramref name="sourceLine"/>
    /// to the top of the viewport in the currently visible (active) buffer.
    /// </summary>
    public bool AlignTopToSourceLine(int sourceLine)
    {
        var control = FindNearestAtOrBefore(ActiveLineMap, sourceLine);
        if (control is null)
        {
            return false;
        }
        var pos = control.TranslatePoint(default, _bufferGrid);
        if (!pos.HasValue)
        {
            return false;
        }
        Scroll.Offset = new Vector(Scroll.Offset.X, pos.Value.Y);
        return true;
    }

    /// <summary>
    /// Returns the source line of the top-most visible preview block in the
    /// currently visible (active) buffer.
    /// </summary>
    public int? FirstVisibleSourceLine()
    {
        return FirstVisibleSourceLineIn(ActiveLineMap);
    }

    /// <summary>
    /// Returns the source line of the block whose vertical center is closest
    /// to the center of the viewport. Used by sync-scroll: tying the middle
    /// stays accurate across height mismatches the way top/bottom anchors
    /// don't.
    /// </summary>
    public int? CenterVisibleSourceLine()
    {
        var viewportCenter = Scroll.Offset.Y + (Scroll.Viewport.Height / 2.0);
        var bestLine = -1;
        var bestDistance = double.MaxValue;
        foreach (var pair in ActiveLineMap)
        {
            var pos = pair.Value.TranslatePoint(default, _bufferGrid);
            if (!pos.HasValue)
            {
                continue;
            }
            var controlCenter = pos.Value.Y + (pair.Value.Bounds.Height / 2.0);
            var distance = Math.Abs(controlCenter - viewportCenter);
            if (distance >= bestDistance)
            {
                continue;
            }
            bestDistance = distance;
            bestLine = pair.Key;
        }
        return bestLine < 0 ? null : bestLine;
    }

    /// <summary>
    /// Aligns the block at or before <paramref name="sourceLine"/> so its
    /// vertical center sits at the viewport's vertical center.
    /// </summary>
    public bool AlignCenterToSourceLine(int sourceLine)
    {
        var control = FindNearestAtOrBefore(ActiveLineMap, sourceLine);
        if (control is null)
        {
            return false;
        }
        var pos = control.TranslatePoint(default, _bufferGrid);
        if (!pos.HasValue)
        {
            return false;
        }
        var controlCenter = pos.Value.Y + (control.Bounds.Height / 2.0);
        var target = controlCenter - (Scroll.Viewport.Height / 2.0);
        var maxY = Math.Max(0, Scroll.Extent.Height - Scroll.Viewport.Height);
        Scroll.Offset = new Vector(Scroll.Offset.X, Math.Clamp(target, 0, maxY));
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

    // True only when the rendered buffer is still empty and real content has
    // arrived: the initial document load. Every later edit (or clearing the
    // buffer) is debounced, since only the first paint is latency sensitive.
    internal static bool ShouldRenderImmediately(string? lastRendered, string pending)
    {
        return string.IsNullOrEmpty(lastRendered) && !string.IsNullOrEmpty(pending);
    }

    // The window keeps one preview per view mode (source/preview/split) in the
    // tree at once, toggled through their container's IsVisible. A hidden copy
    // still receives Source changes, so without this gate every copy would build
    // the full control tree on each document change. Effective visibility (not
    // the copy's own IsVisible, which stays true inside a hidden container) is
    // the right signal. Defer hidden panels; paint the first real content at
    // once; debounce the rest.
    internal static PreviewRenderSchedule DecidePreviewRender(
        bool isEffectivelyVisible,
        string? lastRendered,
        string pending
    )
    {
        if (!isEffectivelyVisible)
        {
            return PreviewRenderSchedule.Defer;
        }
        return ShouldRenderImmediately(lastRendered, pending)
            ? PreviewRenderSchedule.Immediate
            : PreviewRenderSchedule.Debounced;
    }

    // Brings the block tagged with the given heading id into view. Called by the
    // renderer when an in-document anchor link is clicked, scoped to this
    // preview instance so split-view panes don't all scroll together.
    internal void ScrollToAnchor(string anchorId)
    {
        foreach (var child in ActivePanel.Children)
        {
            if (child is Control c && c.Tag is string id && string.Equals(id, anchorId, StringComparison.Ordinal))
            {
                c.BringIntoView();
                return;
            }
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

    // Focus the preview on an empty-area click so Cmd+F targets it, but do not
    // steal focus when the press lands on a text block: that focus theft cancels
    // the SelectableTextBlock's mouse selection before it starts. Cmd+F still
    // resolves this preview by walking up from the focused text block.
    private static void OnSurfacePointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is MarkdownPreviewControl preview && e.Source is not SelectableTextBlock)
        {
            preview.Focus();
        }
    }

    // Make every rendered text block actually selectable: in this Avalonia build
    // a SelectableTextBlock will not start a mouse selection unless it is
    // focusable, and the selection stays invisible without an explicit brush. The
    // selector matches LinkInlineTextBlock (paragraphs, headings) as well as the
    // plain SelectableTextBlocks (code, quotes, table cells).
    private static Style BuildSelectableTextStyle()
    {
        return new Style(x => x.Is<SelectableTextBlock>())
        {
            Setters =
            {
                new Setter(Avalonia.Input.InputElement.FocusableProperty, true),
                new Setter(
                    SelectableTextBlock.SelectionBrushProperty,
                    new SolidColorBrush(Color.FromArgb(0x66, 0x4D, 0x9D, 0xF0))
                ),
            },
        };
    }

    private static Control? FindNearestAtOrBefore(Dictionary<int, Control> map, int sourceLine)
    {
        if (map.TryGetValue(sourceLine, out var exact))
        {
            return exact;
        }
        var bestLine = -1;
        Control? best = null;
        foreach (var pair in map)
        {
            if (pair.Key > sourceLine || pair.Key <= bestLine)
            {
                continue;
            }
            bestLine = pair.Key;
            best = pair.Value;
        }
        return best;
    }

    private int? FirstVisibleSourceLineIn(Dictionary<int, Control> map)
    {
        var offsetY = Scroll.Offset.Y;
        var bestLine = -1;
        var bestDistance = double.MaxValue;
        foreach (var pair in map)
        {
            var pos = pair.Value.TranslatePoint(default, _bufferGrid);
            if (!pos.HasValue)
            {
                continue;
            }
            var distance = pos.Value.Y - offsetY;
            if (distance < 0)
            {
                // Block starts above the viewport; treat the closest one as
                // the top so partial visibility still maps.
                distance = -distance + double.Epsilon;
            }
            if (distance >= bestDistance)
            {
                continue;
            }
            bestDistance = distance;
            bestLine = pair.Key;
        }
        return bestLine < 0 ? null : bestLine;
    }

    private void ApplySearchOnActive()
    {
        _searcher.Highlight(ActiveLineMap.Values, SearchTerm ?? string.Empty, SearchCaseSensitive);
        if (_searcher.MatchCount > 0)
        {
            PreviewSearcher.FindControlParent(_searcher.ActiveMatch?.Parent)?.BringIntoView();
        }
    }

    private void OnEffectiveViewportChanged(object? sender, Avalonia.Layout.EffectiveViewportChangedEventArgs e)
    {
        if (IsEffectivelyVisible && _renderDeferredWhileHidden)
        {
            // This view-mode copy just became active: paint the content that
            // changed while it was hidden.
            ScheduleRender(Source ?? string.Empty);
        }
    }

    private void ScheduleRender(string source)
    {
        _pendingSource = source;
        switch (DecidePreviewRender(IsEffectivelyVisible, _lastRenderedSource, source))
        {
            case PreviewRenderSchedule.Defer:
                _renderDeferredWhileHidden = true;
                return;
            case PreviewRenderSchedule.Immediate:
                // First real paint: skip the typing debounce so opening a
                // document shows content immediately instead of lagging by
                // DebounceMs.
                _renderDeferredWhileHidden = false;
                _debounceTimer.Stop();
                _ = DoRenderLoopAsync();
                return;
            default:
                _renderDeferredWhileHidden = false;
                _debounceTimer.Stop();
                _debounceTimer.Start();
                if (!_forceTimer.IsEnabled)
                {
                    _forceTimer.Start();
                }
                return;
        }
    }

    private void OnDebounceElapsed(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        _ = DoRenderLoopAsync();
    }

    private void OnForceElapsed(object? sender, EventArgs e)
    {
        _ = DoRenderLoopAsync();
    }

    private async System.Threading.Tasks.Task DoRenderLoopAsync()
    {
        if (_renderBusy)
        {
            return;
        }
        _renderBusy = true;
        try
        {
            while (
                !App.ShutdownToken.IsCancellationRequested
                && (
                    !string.Equals(_pendingSource, _lastRenderedSource, StringComparison.Ordinal)
                    || _renderGeneration != _lastRenderedGeneration
                )
            )
            {
                var source = _pendingSource;
                _lastRenderedSource = source;
                _lastRenderedGeneration = _renderGeneration;
                await RenderAsync(source);
            }
            _forceTimer.Stop();
        }
        catch (OperationCanceledException)
        {
            // Shutdown interrupted the render loop.
        }
        finally
        {
            _renderBusy = false;
        }
    }

    private async System.Threading.Tasks.Task RenderAsync(string source)
    {
        RenderStarted?.Invoke(this, EventArgs.Empty);
        try
        {
            var document = await System.Threading.Tasks.Task.Run(
                () => MarkdownPipeline.Parse(source),
                App.ShutdownToken
            );
            if (document is null)
            {
                return;
            }
            PrepareThemeAndPending(document.Count);
            StreamIntoPending(document);
            // Snapshot anchor line BEFORE swap so we know where the user was
            // looking. ActiveLineMap is the currently visible buffer.
            var anchorLine = FirstVisibleSourceLineIn(ActiveLineMap);
            // Force layout so pending's children are arranged and
            // TranslatePoint returns valid coordinates.
            _bufferGrid.UpdateLayout();
            var anchorY = ResolveAnchorY(anchorLine);
            SwapBuffersAndAnchor(anchorY);
            ApplySearchOnActive();
        }
        finally
        {
            RenderCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void PrepareThemeAndPending(int blockHint)
    {
        PendingPanel.IsVisible = true;
        PendingPanel.Opacity = 0;
        PendingPanel.IsHitTestVisible = false;
        PendingPanel.Children.Clear();
        PendingLineMap.Clear();
        PendingLineMap.EnsureCapacity(blockHint + 16);

        var theme = MarkdownRenderer.Theme;
        Background = new SolidColorBrush(theme.Background);
        Scroll.HorizontalScrollBarVisibility = MarkdownRenderer.WrapCode
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Auto;
    }

    private void StreamIntoPending(Markdig.Syntax.MarkdownDocument document)
    {
        // Build the whole pending buffer synchronously; the user keeps seeing
        // the active buffer until the swap, so yielding only delays the swap.
        foreach (var block in MarkdownRenderer.Render(document))
        {
            PendingPanel.Children.Add(block.Control);
            PendingLineMap[block.SourceLine] = block.Control;
        }
    }

    private double? ResolveAnchorY(int? anchorLine)
    {
        if (anchorLine is not { } line)
        {
            return null;
        }
        var target = FindNearestAtOrBefore(PendingLineMap, line);
        if (target is null)
        {
            return null;
        }
        var pos = target.TranslatePoint(default, _bufferGrid);
        return pos?.Y;
    }

    private void SwapBuffersAndAnchor(double? anchorY)
    {
        // Atomic on the UI thread: opacity + IsVisible + offset write happen
        // in one synchronous batch, so the next composition frame shows the
        // final state. UpdateLayout after the visibility swap ensures the
        // ScrollViewer's Extent reflects the new visible buffer before we
        // write the offset (otherwise it could clamp the target Y to 0).
        ActivePanel.Opacity = 0;
        ActivePanel.IsVisible = false;
        ActivePanel.IsHitTestVisible = false;
        PendingPanel.Opacity = 1;
        PendingPanel.IsHitTestVisible = true;
        _activeIsA = !_activeIsA;
        _bufferGrid.UpdateLayout();
        if (anchorY is { } y)
        {
            var maxY = Math.Max(0, Scroll.Extent.Height - Scroll.Viewport.Height);
            Scroll.Offset = new Vector(Scroll.Offset.X, Math.Min(y, maxY));
        }
    }
}
