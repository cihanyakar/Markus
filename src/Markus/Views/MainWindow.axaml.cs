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

    // Track the last ratio we pushed to each side; an incoming ScrollChanged
    // that matches the last push is our own programmatic write and skipped.
    // Flag-based guards weren't reliable because Avalonia raises ScrollChanged
    // on a later layout pass, after the flag reset.
    private double _lastEditorRatio = double.NaN;
    private double _lastPreviewRatio = double.NaN;

    public MainWindow()
    {
        InitializeComponent();
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
    }

    private void WireScrollSync()
    {
        // Bind ScrollLock toggle to both the physical Scroll Lock key and an
        // alt shortcut (Cmd+Shift+L) for keyboards without Scroll Lock.
        KeyBindings.Add(
            new KeyBinding { Gesture = new KeyGesture(Key.Scroll), Command = new RelayCommand(InvokeScrollLockToggle) }
        );
        KeyBindings.Add(
            new KeyBinding
            {
                Gesture = new KeyGesture(Key.L, KeyModifiers.Meta | KeyModifiers.Shift),
                Command = new RelayCommand(InvokeScrollLockToggle),
            }
        );

        Loaded += (_, _) => AttachEditorScrollSync();
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
        }
        AttachPreviewScrollSubscribers();
    }

    private void AttachPreviewScrollSubscribers()
    {
        foreach (var preview in this.GetVisualDescendants().OfType<MarkdownPreviewControl>())
        {
            preview.Scroll.ScrollChanged -= OnPreviewScrollChanged;
            preview.Scroll.ScrollChanged += OnPreviewScrollChanged;
        }
        if (_previewWindow?.FindDescendantPreview() is { } detachedPreview)
        {
            detachedPreview.Scroll.ScrollChanged -= OnPreviewScrollChanged;
            detachedPreview.Scroll.ScrollChanged += OnPreviewScrollChanged;
        }
    }

    private void OnEditorScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !vm.IsScrollLocked)
        {
            return;
        }
        if (sender is not ScrollViewer source)
        {
            return;
        }
        var ratio = GetRatio(source);
        if (IsEcho(ratio, _lastEditorRatio))
        {
            return;
        }
        _lastEditorRatio = ratio;
        var preview = VisiblePreviewFor(vm.CurrentViewMode);
        if (preview is null)
        {
            return;
        }
        _lastPreviewRatio = ratio;
        ApplyRatio(preview.Scroll, ratio);
    }

    private void OnPreviewScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !vm.IsScrollLocked)
        {
            return;
        }
        if (sender is not ScrollViewer source)
        {
            return;
        }
        var ratio = GetRatio(source);
        if (IsEcho(ratio, _lastPreviewRatio))
        {
            return;
        }
        _lastPreviewRatio = ratio;
        var editor = VisibleEditorFor(vm.CurrentViewMode);
        var target = editor is null ? null : FindEditorScrollViewer(editor);
        if (target is null)
        {
            return;
        }
        _lastEditorRatio = ratio;
        ApplyRatio(target, ratio);
    }

    private static bool IsEcho(double current, double lastPushed)
    {
        return !double.IsNaN(lastPushed) && Math.Abs(current - lastPushed) < 0.0005;
    }

    private static void ApplyRatio(ScrollViewer target, double ratio)
    {
        var max = target.Extent.Height - target.Viewport.Height;
        if (max <= 0)
        {
            return;
        }
        target.Offset = new Vector(target.Offset.X, max * ratio);
    }

    private static double GetRatio(ScrollViewer sv)
    {
        var max = sv.Extent.Height - sv.Viewport.Height;
        return max > 0 ? sv.Offset.Y / max : 0;
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
        KeyBindings.Add(
            new KeyBinding
            {
                Gesture = new KeyGesture(Key.K, KeyModifiers.Meta),
                Command = new RelayCommand(ShowCommandPalette),
            }
        );
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
        return new[]
        {
            new Markus.Models.CommandItem("Open File", "File", "⌘O", () => vm.OpenFileCommand.Execute(null)),
            new Markus.Models.CommandItem("Reload", "File", "⌘R", () => vm.ReloadCommand.Execute(null)),
            new Markus.Models.CommandItem("Settings", "App", "⌘,", () => vm.OpenSettingsCommand.Execute(null)),
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
            new Markus.Models.CommandItem("Find in Document", "Search", "⌘F", RouteSearchToActiveSurface),
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

        KeyBindings.Add(
            new KeyBinding
            {
                Gesture = new KeyGesture(Key.F, KeyModifiers.Meta),
                Command = new RelayCommand(RouteSearchToActiveSurface),
            }
        );
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
            if (!string.IsNullOrEmpty(path))
            {
                await vm.LoadFileAsync(path);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Drop failed: {ex.Message}");
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            vm.OpenRequested += OnOpenRequested;
            vm.SettingsRequested += OnSettingsRequested;
            UpdateDetachedWindows(vm);
            RefreshRecentMenu();
            ApplyEditorWordWrap(vm.IsSourceSoftWrap);
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
        _previewWindow = new DetachedPreviewWindow { DataContext = vm };
        _previewWindow.Closed += (_, _) => _previewWindow = null;
        _previewWindow.Show(this);
    }

    private void CloseDetachedWindows()
    {
        _previewWindow?.Close();
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
        CloseDetachedWindows();
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.OpenRequested -= OnOpenRequested;
            vm.SettingsRequested -= OnSettingsRequested;
            vm.Dispose();
        }
    }

    private void OutlineTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0)
        {
            return;
        }
        if (e.AddedItems[0] is not Markus.Models.OutlineNode node)
        {
            return;
        }
        ScrollVisiblePreview(node.SourceLine);
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
