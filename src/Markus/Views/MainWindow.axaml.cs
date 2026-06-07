using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Markus.Services;
using Markus.ViewModels;
using Markus.Views.Platform;

namespace Markus.Views;

internal sealed partial class MainWindow : Window
{
    private DetachedPreviewWindow? _previewWindow;
    private MarkdownTextEditor? _searchTargetEditor;
    private MarkdownPreviewControl? _searchTargetPreview;

    // Sync-scroll uses source line numbers in both directions (folds make
    // pixel ratios diverge). A single suppression flag reset via background
    // dispatcher priority blocks the echo ScrollChanged Avalonia raises on
    // the next layout pass, which a synchronous flag missed.
    private bool _syncingScroll;
    private int _suppressedRenderCount;

    // Set once the user has resolved the unsaved-changes prompt so the second,
    // programmatic Close() pass runs the real shutdown instead of re-prompting.
    private bool _forceClose;

    // True while we close the detached preview ourselves (on a view-mode switch)
    // so its Closed handler doesn't mistake it for the user closing the window.
    private bool _closingPreviewProgrammatically;

    public MainWindow()
    {
        InitializeComponent();
        Icon = Services.IconLoader.LoadWindowIcon();
        Closing += OnWindowClosing;
        DataContextChanged += OnDataContextChanged;

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        DragDrop.SetAllowDrop(this, true);

        ConfigureExtendedTitleBar();
        Opened += OnWindowOpened;
        WirePreviewSearch();
        WireCommandPalette();
        WireScrollSync();
        WireOutlinePanels();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from process-lifetime singletons unconditionally and once,
        // independent of the VM-gated teardown in OnWindowClosing. Closing can be
        // cancelled/re-raised and may be skipped on some app-quit paths, so a
        // leaked handler on ServiceLocator.Keys (an app singleton) would otherwise
        // pin this window for the process lifetime. -= is a no-op if already done.
        Services.ServiceLocator.Keys.Changed -= OnKeyBindingsChanged;
        if (this.FindControl<OutlinePanel>("OutlineLeft") is { } left)
        {
            left.NodeSelected -= OnOutlineNodeSelected;
        }
        if (this.FindControl<OutlinePanel>("OutlineRight") is { } right)
        {
            right.NodeSelected -= OnOutlineNodeSelected;
        }
        base.OnClosed(e);
    }

    private void WireOutlinePanels()
    {
        if (this.FindControl<OutlinePanel>("OutlineLeft") is { } left)
        {
            left.NodeSelected += OnOutlineNodeSelected;
        }
        if (this.FindControl<OutlinePanel>("OutlineRight") is { } right)
        {
            right.NodeSelected += OnOutlineNodeSelected;
        }
    }

    private void WireScrollSync()
    {
        // Physical Scroll Lock toggles isolated from the keybinding service.
        // No Meta modifier means it won't ever conflict with user-rebound
        // shortcuts and laptop keyboards without the key just ignore it.
        KeyBindings.Add(
            new KeyBinding { Gesture = new KeyGesture(Key.Scroll), Command = new RelayCommand(InvokeScrollLockToggle) }
        );
        ApplyKeyBindings();
        Services.ServiceLocator.Keys.Changed += OnKeyBindingsChanged;
        Loaded += (_, _) => AttachEditorScrollSync();
    }

    private void OnKeyBindingsChanged(object? sender, EventArgs e)
    {
        ApplyKeyBindings();
    }

    private void ApplyKeyBindings()
    {
        // Wipe the dynamic bindings (everything except the special Scroll
        // Lock key set in WireScrollSync) and rebuild from the catalog so a
        // user edit in Settings → Shortcuts takes effect immediately.
        for (var i = KeyBindings.Count - 1; i >= 0; i--)
        {
            if (KeyBindings[i].Gesture is { } g && g.Key == Key.Scroll && g.KeyModifiers == KeyModifiers.None)
            {
                continue;
            }
            KeyBindings.RemoveAt(i);
        }
        var keys = Services.ServiceLocator.Keys;
        foreach (var action in Services.ShortcutActions.All)
        {
            var gesture = keys.GetGesture(action);
            if (gesture is null)
            {
                continue;
            }
            var command = ResolveCommand(action);
            if (command is null)
            {
                continue;
            }
            KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = command });
        }
    }

    private ICommand? ResolveCommand(Services.ShortcutAction action)
    {
        // Map catalog ids to the ViewModel commands / window helpers they
        // already trigger. Returning null skips the binding (e.g., the
        // ViewModel isn't ready yet on first construction).
        if (DataContext is not MainWindowViewModel vm)
        {
            return null;
        }
        return action.Id switch
        {
            "file.open" => vm.OpenFileCommand,
            "file.save" => vm.SaveCommand,
            "file.reload" => vm.ReloadCommand,
            "file.new" => vm.NewScratchCommand,
            "edit.find" => vm.FindCommand,
            "edit.find-next" => vm.FindNextCommand,
            "edit.find-previous" => vm.FindPreviousCommand,
            "edit.format-tables" => vm.FormatTablesCommand,
            "view.toggle-outline" => vm.ToggleOutlineCommand,
            "view.scroll-lock" => new RelayCommand(InvokeScrollLockToggle),
            "view.focus-mode" => new RelayCommand(InvokeFocusToggle),
            "view.source" => new RelayCommand(() => vm.SetViewModeCommand.Execute(Markus.Models.ViewMode.Source)),
            "view.preview" => new RelayCommand(() => vm.SetViewModeCommand.Execute(Markus.Models.ViewMode.Preview)),
            "view.split-vertical" => new RelayCommand(() =>
                vm.SetViewModeCommand.Execute(Markus.Models.ViewMode.SplitVertical)
            ),
            "view.split-horizontal" => new RelayCommand(() =>
                vm.SetViewModeCommand.Execute(Markus.Models.ViewMode.SplitHorizontal)
            ),
            "view.detached" => new RelayCommand(() => vm.SetViewModeCommand.Execute(Markus.Models.ViewMode.Detached)),
            "tools.command-palette" => new RelayCommand(ShowCommandPalette),
            _ => null,
        };
    }

    private void InvokeFocusToggle()
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ToggleFocusModeCommand.Execute(null);
        }
    }

    private void InvokeScrollLockToggle()
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ToggleScrollLockCommand.Execute(null);
        }
    }

    private void AttachEditorScrollSync()
    {
        // Drive sync from both sides via the wrapping ScrollViewer's
        // ScrollChanged event. Ratio-based positioning makes the scroll
        // pixel-smooth instead of jumping block-to-block.
        foreach (var editor in this.GetVisualDescendants().OfType<MarkdownTextEditor>())
        {
            if (FindEditorScrollViewer(editor) is not { } sv)
            {
                continue;
            }
            sv.ScrollChanged -= OnEditorScrollChanged;
            sv.ScrollChanged += OnEditorScrollChanged;
            // Caret line/column drives the footer's "Ln X, Col Y" readout.
            editor.TextArea.Caret.PositionChanged -= OnEditorCaretChanged;
            editor.TextArea.Caret.PositionChanged += OnEditorCaretChanged;
            editor.TextArea.SelectionChanged -= OnEditorSelectionChanged;
            editor.TextArea.SelectionChanged += OnEditorSelectionChanged;
            UpdateCaretPosition(editor);
            UpdateSelectionStats(editor);
        }
        AttachPreviewScrollSubscribers();
    }

    private void OnEditorCaretChanged(object? sender, EventArgs e)
    {
        if (sender is not AvaloniaEdit.Editing.Caret caret)
        {
            return;
        }
        UpdateCaretPosition(caret.Line, caret.Column);
    }

    private void UpdateCaretPosition(MarkdownTextEditor editor)
    {
        UpdateCaretPosition(editor.TextArea.Caret.Line, editor.TextArea.Caret.Column);
    }

    private void UpdateCaretPosition(int line, int column)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CaretPosition = $"Ln {line}, Col {column}";
        }
    }

    private void OnEditorSelectionChanged(object? sender, EventArgs e)
    {
        if (sender is not AvaloniaEdit.Editing.TextArea area)
        {
            return;
        }
        var editor = area.GetVisualAncestors().OfType<MarkdownTextEditor>().FirstOrDefault();
        if (editor is null)
        {
            return;
        }
        UpdateSelectionStats(editor);
    }

    private void UpdateSelectionStats(MarkdownTextEditor editor)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }
        var selectionText = editor.TextArea.Selection.GetText();
        vm.SelectionStats = string.IsNullOrEmpty(selectionText)
            ? string.Empty
            : $"{CountWords(selectionText)} selected · {selectionText.Length} chars";
    }

    private static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                inWord = false;
                continue;
            }
            if (!inWord)
            {
                inWord = true;
                count++;
            }
        }
        return count;
    }

    private void AttachPreviewScrollSubscribers()
    {
        foreach (var preview in this.GetVisualDescendants().OfType<MarkdownPreviewControl>())
        {
            preview.Scroll.ScrollChanged -= OnPreviewScrollChanged;
            preview.Scroll.ScrollChanged += OnPreviewScrollChanged;
            preview.RenderStarted -= OnPreviewRenderStarted;
            preview.RenderStarted += OnPreviewRenderStarted;
            preview.RenderCompleted -= OnPreviewRenderCompleted;
            preview.RenderCompleted += OnPreviewRenderCompleted;
        }
        if (_previewWindow?.FindDescendantPreview() is { } detachedPreview)
        {
            detachedPreview.Scroll.ScrollChanged -= OnPreviewScrollChanged;
            detachedPreview.Scroll.ScrollChanged += OnPreviewScrollChanged;
            detachedPreview.RenderStarted -= OnPreviewRenderStarted;
            detachedPreview.RenderStarted += OnPreviewRenderStarted;
            detachedPreview.RenderCompleted -= OnPreviewRenderCompleted;
            detachedPreview.RenderCompleted += OnPreviewRenderCompleted;
        }
    }

    private void OnPreviewRenderStarted(object? sender, EventArgs e)
    {
        _suppressedRenderCount++;
    }

    private void OnPreviewRenderCompleted(object? sender, EventArgs e)
    {
        if (_suppressedRenderCount > 0)
        {
            _suppressedRenderCount--;
        }
    }

    private void OnEditorScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (
            _syncingScroll
            || _suppressedRenderCount > 0
            || DataContext is not MainWindowViewModel vm
            || !vm.IsScrollLocked
        )
        {
            return;
        }
        if (sender is not ScrollViewer source)
        {
            return;
        }
        var editor = source.GetVisualAncestors().OfType<MarkdownTextEditor>().FirstOrDefault();
        if (editor is null)
        {
            return;
        }
        var centerLine = EditorCenterSourceLine(editor, source);
        if (centerLine is null)
        {
            return;
        }
        var preview = VisiblePreviewFor(vm.CurrentViewMode);
        if (preview is null)
        {
            return;
        }
        BeginSyncWindow();
        preview.AlignCenterToSourceLine(centerLine.Value);
    }

    private void OnPreviewScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (
            _syncingScroll
            || _suppressedRenderCount > 0
            || DataContext is not MainWindowViewModel vm
            || !vm.IsScrollLocked
        )
        {
            return;
        }
        if (sender is not ScrollViewer)
        {
            return;
        }
        var preview = VisiblePreviewFor(vm.CurrentViewMode);
        if (preview is null)
        {
            return;
        }
        var line = preview.CenterVisibleSourceLine();
        if (line is null)
        {
            return;
        }
        var editor = VisibleEditorFor(vm.CurrentViewMode);
        if (editor is null)
        {
            return;
        }
        BeginSyncWindow();
        ScrollEditorCenterToLine(editor, line.Value);
    }

    private static int? EditorCenterSourceLine(MarkdownTextEditor editor, ScrollViewer sv)
    {
        var view = editor.TextArea.TextView;
        var visualLines = view.VisualLines;
        if (visualLines.Count == 0)
        {
            return null;
        }
        var centerY = sv.Offset.Y + (sv.Viewport.Height / 2.0);
        foreach (var vl in visualLines)
        {
            if (centerY >= vl.VisualTop && centerY < vl.VisualTop + vl.Height)
            {
                return vl.FirstDocumentLine.LineNumber - 1;
            }
        }
        // Fallback: pick the visually middle line if no exact hit (e.g., centerY
        // landed in inter-line padding).
        return visualLines[visualLines.Count / 2].FirstDocumentLine.LineNumber - 1;
    }

    private static void ScrollEditorCenterToLine(MarkdownTextEditor editor, int sourceLine)
    {
        var doc = editor.Document;
        if (doc is null || doc.LineCount == 0)
        {
            return;
        }
        var lineNumber = Math.Clamp(sourceLine + 1, 1, doc.LineCount);
        // ScrollToLine constructs the VisualLine if it isn't already realized
        // so VisualTop is meaningful below.
        editor.ScrollToLine(lineNumber);
        var view = editor.TextArea.TextView;
        var vl = view.GetVisualLine(lineNumber);
        if (vl is null)
        {
            return;
        }
        var sv = FindEditorScrollViewer(editor);
        if (sv is null)
        {
            return;
        }
        var lineCenterY = vl.VisualTop + (vl.Height / 2.0);
        var target = lineCenterY - (sv.Viewport.Height / 2.0);
        var maxY = Math.Max(0, sv.Extent.Height - sv.Viewport.Height);
        sv.Offset = new Vector(sv.Offset.X, Math.Clamp(target, 0, maxY));
    }

    private void BeginSyncWindow()
    {
        // Stay in "syncing" state until the dispatcher has drained input and
        // layout passes from this iteration; the echo ScrollChanged Avalonia
        // raises after a programmatic Offset write fires inside that window.
        _syncingScroll = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _syncingScroll = false,
            Avalonia.Threading.DispatcherPriority.Background
        );
    }

    private MarkdownPreviewControl? VisiblePreviewFor(Markus.Models.ViewMode mode)
    {
        if (mode == Markus.Models.ViewMode.Detached)
        {
            return _previewWindow?.FindDescendantPreview();
        }
        foreach (var preview in this.GetVisualDescendants().OfType<MarkdownPreviewControl>())
        {
            if (preview.IsEffectivelyVisible)
            {
                return preview;
            }
        }
        return null;
    }

    private MarkdownTextEditor? VisibleEditorFor(Markus.Models.ViewMode mode)
    {
        // mode reserved for future per-mode disambiguation; today we just pick
        // the first effectively-visible editor since only one is shown at a time.
        _ = mode;
        foreach (var editor in this.GetVisualDescendants().OfType<MarkdownTextEditor>())
        {
            if (editor.IsEffectivelyVisible)
            {
                return editor;
            }
        }
        return null;
    }

    private static ScrollViewer? FindEditorScrollViewer(MarkdownTextEditor editor)
    {
        // AvaloniaEdit hosts its scrollable area as a descendant ScrollViewer
        // inside the templated TextEditor; the public surface doesn't expose
        // it directly so we walk the visual tree once.
        return editor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
    }

    private void WireCommandPalette()
    {
        var palette = this.FindControl<CommandPalette>("CommandPalette");
        if (palette is null)
        {
            return;
        }
        palette.CloseRequested += (_, _) => HideCommandPalette();
        // Cmd+K gesture lives in the shared catalog (tools.command-palette)
        // so it can be remapped from Settings → Shortcuts.
    }

    private void ShowCommandPalette()
    {
        var palette = this.FindControl<CommandPalette>("CommandPalette");
        if (palette is null || DataContext is not MainWindowViewModel vm)
        {
            return;
        }
        palette.SetItems(BuildCommands(vm));
        palette.Query = null;
        palette.IsVisible = true;
        palette.FocusInput();
    }

    private void HideCommandPalette()
    {
        var palette = this.FindControl<CommandPalette>("CommandPalette");
        if (palette is null)
        {
            return;
        }
        palette.IsVisible = false;
        palette.Query = null;
    }

    private IReadOnlyList<Markus.Models.CommandItem> BuildCommands(MainWindowViewModel vm)
    {
        var commands = new List<Markus.Models.CommandItem>(16);
        commands.AddRange(BuildFileCommands(vm));
        commands.AddRange(BuildViewCommands(vm));
        commands.AddRange(BuildLayoutCommands(vm));
        commands.AddRange(BuildThemeCommands(vm));
        commands.Add(new Markus.Models.CommandItem("Find in Document", "Search", "⌘F", RouteSearchToActiveSurface));
        return commands;
    }

    private static Markus.Models.CommandItem[] BuildFileCommands(MainWindowViewModel vm)
    {
        return new[]
        {
            new Markus.Models.CommandItem("Open File", "File", "⌘O", () => vm.OpenFileCommand.Execute(null)),
            new Markus.Models.CommandItem("Save", "File", "⌘S", () => vm.SaveCommand.Execute(null)),
            new Markus.Models.CommandItem("Reload", "File", "⌘R", () => vm.ReloadCommand.Execute(null)),
            new Markus.Models.CommandItem("Settings", "App", "⌘,", () => vm.OpenSettingsCommand.Execute(null)),
        };
    }

    private static Markus.Models.CommandItem[] BuildViewCommands(MainWindowViewModel vm)
    {
        return new[]
        {
            new Markus.Models.CommandItem("Toggle Outline", "View", "⌘⌥B", () => vm.ToggleOutlineCommand.Execute(null)),
            new Markus.Models.CommandItem(
                "Toggle Scroll Lock",
                "View",
                "⌘⇧L",
                () => vm.ToggleScrollLockCommand.Execute(null)
            ),
            new Markus.Models.CommandItem(
                "Toggle Source Soft-Wrap",
                "View",
                null,
                () => vm.ToggleSourceSoftWrapCommand.Execute(null)
            ),
            new Markus.Models.CommandItem(
                "Toggle Preview Soft-Wrap",
                "View",
                null,
                () => vm.TogglePreviewSoftWrapCommand.Execute(null)
            ),
            new Markus.Models.CommandItem(
                "Toggle Focus Mode",
                "View",
                "⌘⇧M",
                () => vm.ToggleFocusModeCommand.Execute(null)
            ),
            new Markus.Models.CommandItem(
                "Toggle Typewriter Mode",
                "View",
                null,
                () => vm.ToggleTypewriterModeCommand.Execute(null)
            ),
        };
    }

    private static Markus.Models.CommandItem[] BuildLayoutCommands(MainWindowViewModel vm)
    {
        return new[]
        {
            new Markus.Models.CommandItem(
                "Source View",
                "View",
                null,
                () => vm.SetViewModeCommand.Execute(Markus.Models.ViewMode.Source)
            ),
            new Markus.Models.CommandItem(
                "Preview View",
                "View",
                null,
                () => vm.SetViewModeCommand.Execute(Markus.Models.ViewMode.Preview)
            ),
            new Markus.Models.CommandItem(
                "Split Vertical",
                "View",
                null,
                () => vm.SetViewModeCommand.Execute(Markus.Models.ViewMode.SplitVertical)
            ),
            new Markus.Models.CommandItem(
                "Split Horizontal",
                "View",
                null,
                () => vm.SetViewModeCommand.Execute(Markus.Models.ViewMode.SplitHorizontal)
            ),
            new Markus.Models.CommandItem(
                "Detached Preview",
                "View",
                null,
                () => vm.SetViewModeCommand.Execute(Markus.Models.ViewMode.Detached)
            ),
        };
    }

    private static Markus.Models.CommandItem[] BuildThemeCommands(MainWindowViewModel vm)
    {
        return new[]
        {
            new Markus.Models.CommandItem(
                "Theme: System",
                "Appearance",
                null,
                () => vm.SetThemeModeCommand.Execute("System")
            ),
            new Markus.Models.CommandItem(
                "Theme: Light",
                "Appearance",
                null,
                () => vm.SetThemeModeCommand.Execute("Light")
            ),
            new Markus.Models.CommandItem(
                "Theme: Dark",
                "Appearance",
                null,
                () => vm.SetThemeModeCommand.Execute("Dark")
            ),
        };
    }

    private void WirePreviewSearch()
    {
        var overlay = this.FindControl<SearchOverlay>("PreviewSearch");
        if (overlay is null)
        {
            return;
        }
        overlay.PropertyChanged += OnSearchOverlayPropertyChanged;
        overlay.Next += (_, _) => MoveSearchMatch(forward: true);
        overlay.Prev += (_, _) => MoveSearchMatch(forward: false);
        overlay.Close += (_, _) => HideSearch();
        overlay.ReplaceCurrent += (_, _) => ReplaceCurrent();
        overlay.ReplaceAll += (_, _) => ReplaceAll();
        // Cmd+F gesture lives in the shared catalog (edit.find) so it can be
        // remapped from Settings → Shortcuts.
    }

    private void OnSearchOverlayPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not SearchOverlay overlay)
        {
            return;
        }
        if (e.Property != SearchOverlay.SearchTextProperty && e.Property != SearchOverlay.CaseSensitiveProperty)
        {
            return;
        }
        ApplySearch(overlay);
    }

    private void RouteSearchToActiveSurface()
    {
        var overlay = this.FindControl<SearchOverlay>("PreviewSearch");
        if (overlay is null || DataContext is not MainWindowViewModel vm)
        {
            return;
        }
        (_searchTargetEditor, _searchTargetPreview) = PickSearchTargets(vm);
        overlay.SupportsReplace = _searchTargetEditor is not null;
        overlay.IsVisible = true;
        overlay.FocusInput();
    }

    private (MarkdownTextEditor? Editor, MarkdownPreviewControl? Preview) PickSearchTargets(MainWindowViewModel vm)
    {
        // Prefer the surface that currently owns keyboard focus.
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if (FindAncestor<MarkdownTextEditor>(focused) is { } focusedEditor)
        {
            return (focusedEditor, null);
        }
        if (FindAncestor<MarkdownPreviewControl>(focused) is { } focusedPreview)
        {
            return (null, focusedPreview);
        }
        // No relevant focus: fall back to the visible surface by view mode.
        if (vm.CurrentViewMode == Markus.Models.ViewMode.Source)
        {
            return (VisibleSourceEditor(), null);
        }
        var preview = ActivePreviewControl();
        return preview is not null ? (null, preview) : (VisibleSourceEditor(), null);
    }

    private MarkdownTextEditor? VisibleSourceEditor()
    {
        foreach (var editor in this.GetVisualDescendants().OfType<MarkdownTextEditor>())
        {
            if (editor.IsVisible)
            {
                return editor;
            }
        }
        return null;
    }

    private static T? FindAncestor<T>(Visual? from)
        where T : class
    {
        for (Visual? cursor = from; cursor is not null; cursor = cursor.GetVisualParent())
        {
            if (cursor is T match)
            {
                return match;
            }
        }
        return null;
    }

    private MarkdownPreviewControl? ActivePreviewControl()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return null;
        }
        return vm.CurrentViewMode switch
        {
            Markus.Models.ViewMode.Preview => PreviewOnlyView,
            Markus.Models.ViewMode.SplitVertical => SplitVPreviewView,
            Markus.Models.ViewMode.SplitHorizontal => SplitHPreviewView,
            _ => null,
        };
    }

    private void HideSearch()
    {
        var overlay = this.FindControl<SearchOverlay>("PreviewSearch");
        if (overlay is null)
        {
            return;
        }
        overlay.IsVisible = false;
        overlay.SearchText = null;
        overlay.ReplaceText = null;
        overlay.MatchCount = 0;
        overlay.ActiveIndex = -1;
        _searchTargetPreview?.SetCurrentValue(MarkdownPreviewControl.SearchTermProperty, null);
        _searchTargetEditor?.CloseSearch();
        _searchTargetEditor = null;
        _searchTargetPreview = null;
    }

    private void ApplySearch(SearchOverlay overlay)
    {
        var term = overlay.SearchText ?? string.Empty;
        if (_searchTargetEditor is { } editor)
        {
            ApplyEditorSearch(editor, overlay, term);
            return;
        }
        if (_searchTargetPreview is { } preview)
        {
            ApplyPreviewSearch(preview, overlay, term);
        }
    }

    private static void ApplyEditorSearch(MarkdownTextEditor editor, SearchOverlay overlay, string term)
    {
        editor.SearchMatchCase = overlay.CaseSensitive;
        editor.SearchPattern = term;
        if (!string.IsNullOrEmpty(term))
        {
            editor.FindNextMatch();
        }
        overlay.MatchCount = editor.CountMatches(term, overlay.CaseSensitive);
        overlay.ActiveIndex = overlay.MatchCount == 0 ? -1 : editor.CurrentMatchIndex(term, overlay.CaseSensitive);
    }

    private static void ApplyPreviewSearch(MarkdownPreviewControl preview, SearchOverlay overlay, string term)
    {
        preview.SearchCaseSensitive = overlay.CaseSensitive;
        preview.SearchTerm = term;
        overlay.MatchCount = preview.MatchCount;
        overlay.ActiveIndex = preview.ActiveMatchIndex;
    }

    private void MoveSearchMatch(bool forward)
    {
        if (_searchTargetEditor is { } editor)
        {
            MoveEditorMatch(editor, forward);
            return;
        }
        if (_searchTargetPreview is { } preview)
        {
            MovePreviewMatch(preview, forward);
        }
    }

    private void MoveEditorMatch(MarkdownTextEditor editor, bool forward)
    {
        Action navigate = forward ? editor.FindNextMatch : editor.FindPreviousMatch;
        navigate();
        if (this.FindControl<SearchOverlay>("PreviewSearch") is not { } overlay)
        {
            return;
        }
        overlay.ActiveIndex = editor.CurrentMatchIndex(overlay.SearchText ?? string.Empty, overlay.CaseSensitive);
    }

    private void MovePreviewMatch(MarkdownPreviewControl preview, bool forward)
    {
        Action navigate = forward ? preview.MoveToNextMatch : preview.MoveToPrevMatch;
        navigate();
        if (this.FindControl<SearchOverlay>("PreviewSearch") is not { } overlay)
        {
            return;
        }
        overlay.ActiveIndex = preview.ActiveMatchIndex;
    }

    private void ReplaceCurrent()
    {
        var overlay = this.FindControl<SearchOverlay>("PreviewSearch");
        if (overlay is null || _searchTargetEditor is null)
        {
            return;
        }
        _searchTargetEditor.Replace(overlay.ReplaceText ?? string.Empty);
    }

    private void ReplaceAll()
    {
        var overlay = this.FindControl<SearchOverlay>("PreviewSearch");
        if (overlay is null || _searchTargetEditor is null)
        {
            return;
        }
        _searchTargetEditor.ReplaceAll(overlay.ReplaceText ?? string.Empty);
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        // Avalonia's AcrylicBlur already inserts an NSVisualEffectView into the
        // window's contentView, but it picks the Big-Sur-deprecated
        // NSVisualEffectMaterialLight. We patch the material so Tahoe applies
        // its current Liquid Glass tint and saturation instead.
        NSVisualEffectInstaller.Patch(this, NSVisualEffectInstaller.Material.HeaderView);
        // Session restore scrolls the editor to LastScrollLine once the file
        // has loaded. Loaded priority runs after layout so the editor's
        // ScrollViewer has its viewport sized.
        Avalonia.Threading.Dispatcher.UIThread.Post(RestoreSessionScroll, Avalonia.Threading.DispatcherPriority.Loaded);
        // Now that the main window is visible, open the detached preview if the
        // app launched directly in Detached mode (deferred from DataContextChanged).
        if (DataContext is MainWindowViewModel vm)
        {
            UpdateDetachedWindows(vm);
        }
        if (Services.StartupTrace.IsEnabled)
        {
            BeginStartupTraceDump();
        }
    }

    // In trace mode, mark the window-opened phase, capture the first preview
    // render, then dump the trace and quit so launch latency can be measured
    // repeatably from a script.
    private void BeginStartupTraceDump()
    {
        Services.StartupTrace.Mark("window-opened");
        // A Render-priority callback runs after the first layout+render pass, so
        // it approximates when the window's first frame actually hits the screen.
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () => Services.StartupTrace.Mark("first-frame"),
            Avalonia.Threading.DispatcherPriority.Render
        );
        foreach (var preview in this.GetVisualDescendants().OfType<MarkdownPreviewControl>())
        {
            void OnFirstRender(object? s, EventArgs args)
            {
                preview.RenderCompleted -= OnFirstRender;
                Services.StartupTrace.Mark("preview-first-render");
            }

            preview.RenderCompleted += OnFirstRender;
        }
        Avalonia.Threading.DispatcherTimer.RunOnce(
            () =>
            {
                Services.StartupTrace.Mark("trace-dump");
                Services.StartupTrace.Dump();
                Environment.Exit(0);
            },
            TimeSpan.FromMilliseconds(700)
        );
    }

    private void RestoreSessionScroll()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }
        var settings = vm.Settings;
        if (settings.LastScrollLine <= 0)
        {
            return;
        }
        if (!string.Equals(vm.CurrentFilePath, settings.LastOpenedFile, StringComparison.Ordinal))
        {
            return;
        }
        foreach (var editor in this.GetVisualDescendants().OfType<MarkdownTextEditor>())
        {
            if (!editor.IsEffectivelyVisible)
            {
                continue;
            }
            editor.ScrollToLine(settings.LastScrollLine);
            break;
        }
    }

    private void ConfigureExtendedTitleBar()
    {
        // On macOS we fold the system title bar into the glass toolbar so the
        // traffic-light buttons float over the toolbar's leading edge. Windows
        // and Linux keep their native chrome until per-platform polish lands.
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }
        // Avalonia 12 removed the ChromeHints enum; ExtendClientAreaToDecorationsHint
        // alone hides system chrome while macOS still floats its traffic-light buttons
        // over the leading edge of the client area.
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = -1;
        WindowControlsInset.Width = 72;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Buttons / combos / textboxes keep their own click semantics.
        if (e.Source is Visual source && IsInteractiveChild(source))
        {
            return;
        }
        // macOS convention: double-click the title bar toggles maximize/normal.
        if (e.ClickCount >= 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            e.Handled = true;
            return;
        }
        BeginMoveDrag(e);
    }

    private static bool IsInteractiveChild(Visual source)
    {
        for (Visual? cursor = source; cursor is not null; cursor = cursor.GetVisualParent())
        {
            if (cursor is Button or ToggleButton or ComboBox or TextBox or Slider or CheckBox)
            {
                return true;
            }
        }
        return false;
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        try
        {
            if (e.DataTransfer is null || DataContext is not MainWindowViewModel vm)
            {
                return;
            }
            var file = e.DataTransfer.TryGetFile();
            if (file is null)
            {
                return;
            }
            var path = file.TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            // Images dropped onto the editor copy into <doc>/assets/ and a
            // Markdown image reference is inserted at the caret. Anything else
            // falls back to the existing "open as document" flow.
            if (IsImageExtension(path))
            {
                // Never open an image as a Markdown document (binary garbage).
                // If there is no editor to insert into, tell the user instead.
                if (!TryInsertImageAtCaret(vm, path))
                {
                    SetStatus("Open a document and show the editor to drop an image into it");
                }
                return;
            }
            await vm.LoadFileAsync(path);
        }
        catch (OperationCanceledException)
        {
            // Shutdown interrupted the load.
        }
        catch (Exception ex)
        {
            SetStatus($"Drop failed: {ex.Message}");
        }
    }

    private static bool IsImageExtension(string path)
    {
        var ext = System.IO.Path.GetExtension(path);
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryInsertImageAtCaret(MainWindowViewModel vm, string sourceImage)
    {
        // We need a document folder to drop the asset next to; if the user
        // hasn't saved a file yet, fall back to the document open flow so we
        // don't silently dump assets in unrelated directories.
        if (string.IsNullOrEmpty(vm.CurrentFilePath))
        {
            return false;
        }
        var docDir = System.IO.Path.GetDirectoryName(vm.CurrentFilePath);
        if (string.IsNullOrEmpty(docDir))
        {
            return false;
        }
        var assetsDir = System.IO.Path.Combine(docDir, "assets");
        System.IO.Directory.CreateDirectory(assetsDir);
        var fileName = System.IO.Path.GetFileName(sourceImage);
        var dest = System.IO.Path.Combine(assetsDir, fileName);
        var relative = $"assets/{fileName}";
        // Avoid clobbering an existing file with the same name.
        if (
            !string.Equals(
                System.IO.Path.GetFullPath(sourceImage),
                System.IO.Path.GetFullPath(dest),
                StringComparison.Ordinal
            )
        )
        {
            if (System.IO.File.Exists(dest))
            {
                var baseName = System.IO.Path.GetFileNameWithoutExtension(fileName);
                var ext = System.IO.Path.GetExtension(fileName);
                var n = 2;
                while (System.IO.File.Exists(dest))
                {
                    fileName = $"{baseName}-{n}{ext}";
                    dest = System.IO.Path.Combine(assetsDir, fileName);
                    relative = $"assets/{fileName}";
                    n++;
                }
            }
            System.IO.File.Copy(sourceImage, dest);
        }
        var alt = System.IO.Path.GetFileNameWithoutExtension(fileName);
        var snippet = $"![{alt}]({relative})";
        foreach (var editor in this.GetVisualDescendants().OfType<MarkdownTextEditor>())
        {
            if (!editor.IsEffectivelyVisible)
            {
                continue;
            }
            editor.Document.Insert(editor.CaretOffset, snippet);
            editor.CaretOffset += snippet.Length;
            SetStatus($"Inserted image · {relative}");
            return true;
        }
        return false;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            vm.Interaction = new MainWindowInteraction(this);
            vm.OpenRequested += OnOpenRequested;
            vm.SettingsRequested += OnSettingsRequested;
            vm.FindRequested += OnFindRequested;
            vm.FindNextRequested += OnFindNextRequested;
            vm.FindPreviousRequested += OnFindPreviousRequested;
            vm.PreviewInvalidated += OnPreviewInvalidated;
            UpdateDetachedWindows(vm);
            RefreshRecentMenu();
            ApplyEditorWordWrap(vm.IsSourceSoftWrap);
            // ResolveCommand needs vm; re-run now that DataContext is set.
            ApplyKeyBindings();
        }
    }

    private void OnFindRequested(object? sender, EventArgs e)
    {
        RouteSearchToActiveSurface();
    }

    private void OnFindNextRequested(object? sender, EventArgs e)
    {
        MoveSearchMatch(forward: true);
    }

    private void OnFindPreviousRequested(object? sender, EventArgs e)
    {
        MoveSearchMatch(forward: false);
    }

    private void OnPreviewInvalidated(object? sender, EventArgs e)
    {
        foreach (var preview in this.GetVisualDescendants().OfType<MarkdownPreviewControl>())
        {
            preview.InvalidateRender();
        }
    }

    private void ApplyEditorWordWrap(bool wrap)
    {
        foreach (var editor in this.GetVisualDescendants().OfType<MarkdownTextEditor>())
        {
            editor.WordWrap = wrap;
        }
    }

    private async void OnOpenRequested(object? sender, EventArgs e)
    {
        try
        {
            await OpenFileAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Open failed: {ex.Message}");
        }
    }

    private async void OnSettingsRequested(object? sender, EventArgs e)
    {
        try
        {
            await OpenSettingsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Settings failed: {ex.Message}");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (
            string.Equals(e.PropertyName, nameof(MainWindowViewModel.CurrentViewMode), StringComparison.Ordinal)
            && DataContext is MainWindowViewModel vm
        )
        {
            UpdateDetachedWindows(vm);
            ApplyEditorWordWrap(vm.IsSourceSoftWrap);
            return;
        }
        if (
            string.Equals(e.PropertyName, nameof(MainWindowViewModel.IsSourceSoftWrap), StringComparison.Ordinal)
            && DataContext is MainWindowViewModel vmWrap
        )
        {
            ApplyEditorWordWrap(vmWrap.IsSourceSoftWrap);
            return;
        }
        if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.DocumentTitle), StringComparison.Ordinal))
        {
            // Title change implies a fresh open; refresh the recent menu.
            RefreshRecentMenu();
        }
    }

    private void UpdateDetachedWindows(MainWindowViewModel vm)
    {
        if (vm.CurrentViewMode == Markus.Models.ViewMode.Detached)
        {
            OpenDetachedWindows(vm);
            return;
        }
        CloseDetachedWindows();
    }

    private void OpenDetachedWindows(MainWindowViewModel vm)
    {
        if (_previewWindow is not null)
        {
            return;
        }
        // Show(owner) throws if the owner isn't visible yet. When the app starts
        // directly in Detached mode this runs from DataContextChanged before the
        // main window is shown, so defer; OnWindowOpened reopens once visible.
        if (!IsVisible)
        {
            return;
        }
        _previewWindow = new DetachedPreviewWindow { DataContext = vm };
        _previewWindow.Closed += OnDetachedPreviewClosed;
        _previewWindow.Show(this);
    }

    private void OnDetachedPreviewClosed(object? sender, EventArgs e)
    {
        // Detach the scroll/render handlers from the closing window's preview so
        // the main window holds no delegate into a torn-down control.
        if (_previewWindow?.FindDescendantPreview() is { } detachedPreview)
        {
            detachedPreview.Scroll.ScrollChanged -= OnPreviewScrollChanged;
            detachedPreview.RenderStarted -= OnPreviewRenderStarted;
            detachedPreview.RenderCompleted -= OnPreviewRenderCompleted;
        }
        _previewWindow = null;
        // If the user closed the floating preview themselves, leave Detached
        // mode (otherwise CurrentViewMode stays Detached, the toolbar still
        // shows it active, and re-selecting it can't reopen the window because
        // the value never changes). A programmatic close from a mode switch
        // already set the new mode, so skip it.
        if (_closingPreviewProgrammatically)
        {
            return;
        }
        if (DataContext is MainWindowViewModel vm && vm.CurrentViewMode == Markus.Models.ViewMode.Detached)
        {
            vm.CurrentViewMode = Markus.Models.ViewMode.Preview;
        }
    }

    private void CloseDetachedWindows()
    {
        if (_previewWindow is null)
        {
            return;
        }
        _closingPreviewProgrammatically = true;
        try
        {
            _previewWindow.Close();
        }
        finally
        {
            _closingPreviewProgrammatically = false;
        }
    }

    private async void Open_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await OpenFileAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Open failed: {ex.Message}");
        }
    }

    private void RefreshRecentMenu()
    {
        var openRecent = FindOpenRecentMenuItem();
        if (openRecent?.Menu is not NativeMenu menu || DataContext is not MainWindowViewModel vm)
        {
            return;
        }
        menu.Items.Clear();
        if (vm.Settings.RecentFiles.Count == 0)
        {
            menu.Items.Add(new NativeMenuItem { Header = "No recent files", IsEnabled = false });
            return;
        }
        foreach (var path in vm.Settings.RecentFiles)
        {
            var item = new NativeMenuItem
            {
                Header = System.IO.Path.GetFileName(path),
                ToolTip = path,
                Command = vm.OpenRecentCommand,
                CommandParameter = path,
            };
            menu.Items.Add(item);
        }
    }

    private NativeMenuItem? FindOpenRecentMenuItem()
    {
        var top = NativeMenu.GetMenu(this);
        if (top is null)
        {
            return null;
        }
        foreach (var item in top.Items.OfType<NativeMenuItem>())
        {
            if (item.Menu is null)
            {
                continue;
            }
            foreach (var child in item.Menu.Items.OfType<NativeMenuItem>())
            {
                if (string.Equals(child.Header, "Open Recent", StringComparison.Ordinal))
                {
                    return child;
                }
            }
        }
        return null;
    }

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await OpenSettingsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Settings failed: {ex.Message}");
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Hold the window open while the user decides what to do with unsaved
        // edits; ConfirmCloseAsync re-issues Close() once they've chosen.
        if (!_forceClose && DataContext is MainWindowViewModel dirtyVm && dirtyVm.IsDirty)
        {
            e.Cancel = true;
            _ = ConfirmCloseAsync(dirtyVm);
            return;
        }

        // Any unhandled exception thrown from a window-close handler bubbles
        // up to the AppKit run loop and macOS records the exit as
        // "unexpected quit" in the launch logs. Wrap the cleanup so a stray
        // disposal error doesn't poison the shutdown path.
        try
        {
            CloseDetachedWindows();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Markus shutdown (detached): {ex.Message}");
        }
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }
        try
        {
            // Persist the current scroll line so the next launch can land on
            // the same heading. First-visible source line is the same anchor
            // the sync-scroll engine uses elsewhere.
            var firstLine = ResolveFirstVisibleEditorLine();
            vm.PersistSession(firstLine);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Markus shutdown (session): {ex.Message}");
        }
        try
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.OpenRequested -= OnOpenRequested;
            vm.SettingsRequested -= OnSettingsRequested;
            vm.FindRequested -= OnFindRequested;
            vm.FindNextRequested -= OnFindNextRequested;
            vm.FindPreviousRequested -= OnFindPreviousRequested;
            vm.PreviewInvalidated -= OnPreviewInvalidated;
            vm.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Markus shutdown (dispose): {ex.Message}");
        }
    }

    private async Task ConfirmCloseAsync(MainWindowViewModel vm)
    {
        UnsavedChangesChoice choice;
        try
        {
            choice = await ConfirmDialog.AskDiscardAsync(this, vm.DocumentTitle);
        }
        catch (Exception ex)
        {
            SetStatus($"Close prompt failed: {ex.Message}");
            return;
        }

        if (choice == UnsavedChangesChoice.Cancel)
        {
            return;
        }
        if (choice == UnsavedChangesChoice.Save)
        {
            await vm.SaveCommand.ExecuteAsync(null);
            if (vm.IsDirty)
            {
                // Save-As was dismissed; leave the window open so edits survive.
                return;
            }
        }

        _forceClose = true;
        Close();
    }

    private int ResolveFirstVisibleEditorLine()
    {
        foreach (var editor in this.GetVisualDescendants().OfType<MarkdownTextEditor>())
        {
            var visualLines = editor.TextArea.TextView.VisualLines;
            if (visualLines.Count == 0)
            {
                continue;
            }
            return visualLines[0].FirstDocumentLine.LineNumber;
        }
        return 0;
    }

    private void OnOutlineNodeSelected(object? sender, OutlineNodeSelectedEventArgs e)
    {
        ScrollVisiblePreview(e.Node.SourceLine);
    }

    private void ScrollVisiblePreview(int sourceLine)
    {
        var target = (DataContext as MainWindowViewModel)?.CurrentViewMode switch
        {
            Markus.Models.ViewMode.Preview => PreviewOnlyView,
            Markus.Models.ViewMode.SplitVertical => SplitVPreviewView,
            Markus.Models.ViewMode.SplitHorizontal => SplitHPreviewView,
            _ => null,
        };
        target?.ScrollToLine(sourceLine);
    }

    private void SetStatus(string text)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.StatusText = text;
        }
    }

    private async Task OpenFileAsync()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var pickerOptions = new FilePickerOpenOptions
        {
            Title = "Open Markdown file",
            AllowMultiple = false,
            FileTypeFilter = new FilePickerFileType[]
            {
                new FilePickerFileType("Markdown") { Patterns = new[] { "*.md", "*.markdown", "*.mdown", "*.mkd" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            },
        };

        var files = await StorageProvider.OpenFilePickerAsync(pickerOptions);
        if (files.Count == 0)
        {
            return;
        }

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
        {
            vm.StatusText = "Selected file has no local path (remote storage?)";
            return;
        }

        await vm.LoadFileAsync(path);
    }

    private async Task OpenSettingsAsync()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        // SettingsViewModel auto-saves on every change and SettingsService
        // raises Changed; the main window subscribes in its constructor.
        // The dialog itself is just a "Done" button to dismiss.
        var settingsVm = new SettingsViewModel(ServiceLocator.Settings, vm.Settings);
        var window = new SettingsWindow { DataContext = settingsVm };

        await window.ShowDialog(this);
    }
}
