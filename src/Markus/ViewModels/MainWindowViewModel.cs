using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Markus.Models;
using Markus.Services;

namespace Markus.ViewModels;

internal sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    // Outline rebuild waits this long after the last edit so fast typing does
    // not parse the whole document on every keystroke.
    private const int OutlineDebounceMs = 200;

    private readonly SettingsService _settingsService;
    private readonly FileWatcherService _fileWatcher;
    private System.Threading.CancellationTokenSource? _outlineCts;
    private bool _disposed;
    private bool _suppressOutlinePlacementSave;
    private bool _suppressOutlineVisibleSave;

    // Set while the document is being replaced from disk (open/reload/save) so
    // the resulting SourceText change is not mistaken for a user edit.
    private bool _loadingDocument;

    // The file content as we last read or wrote it. A watcher event whose disk
    // content equals this is our own save (or spurious) rather than an external
    // edit, even if the user has since typed more into the buffer.
    private string _lastSyncedText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSourceOnly))]
    [NotifyPropertyChangedFor(nameof(IsPreviewOnly))]
    [NotifyPropertyChangedFor(nameof(IsSplitVerticalActive))]
    [NotifyPropertyChangedFor(nameof(IsSplitHorizontalActive))]
    [NotifyPropertyChangedFor(nameof(IsDetachedActive))]
    [NotifyPropertyChangedFor(nameof(IsSourceVisibleInMain))]
    private ViewMode _currentViewMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOutlineLeftVisible))]
    [NotifyPropertyChangedFor(nameof(IsOutlineRightVisible))]
    private bool _isOutlineVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOutlineLeftVisible))]
    [NotifyPropertyChangedFor(nameof(IsOutlineRightVisible))]
    private OutlinePlacement _outlinePlacement;

    [ObservableProperty]
    private string _outlineFilter = string.Empty;

    [ObservableProperty]
    private bool _isScrollLocked;

    [ObservableProperty]
    private bool _isSourceSoftWrap;

    [ObservableProperty]
    private bool _isPreviewSoftWrap;

    [ObservableProperty]
    private bool _isFocusMode;

    [ObservableProperty]
    private bool _isTypewriterMode;

    [ObservableProperty]
    private string _statusText = "No file open";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    private string _documentTitle = "Markus";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(IsWelcomeVisible))]
    private string? _currentFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WordCount))]
    [NotifyPropertyChangedFor(nameof(CharCount))]
    [NotifyPropertyChangedFor(nameof(ReadingMinutes))]
    [NotifyPropertyChangedFor(nameof(DocumentStats))]
    private string _sourceText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<OutlineNode> _outlineNodes = Array.Empty<OutlineNode>();

    [ObservableProperty]
    private string _monoFontFamily;

    [ObservableProperty]
    private string _caretPosition = "Ln 1, Col 1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private string _selectionStats = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(IsWelcomeVisible))]
    private bool _isScratchBuffer;

    // True for a brief grace period at launch on macOS, where a double-clicked
    // document is delivered via an AppleEvent that lands just after the window
    // is shown. Suppressing the welcome screen during that window keeps the
    // incoming document from flashing past an empty welcome render.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcomeVisible))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isAwaitingInitialDocument;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DocumentStats))]
    private string _lastModifiedText = string.Empty;

    [ObservableProperty]
    private UpdateViewModel? _update;

    public MainWindowViewModel()
        : this(ServiceLocator.Settings) { }

    public MainWindowViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _fileWatcher = new FileWatcherService();
        _fileWatcher.FileChanged += OnFileChangedOnBackgroundThread;

        Settings = settingsService.Load();
        _currentViewMode = Settings.DefaultViewMode;
        _isOutlineVisible = Settings.ShowOutline;
        _outlinePlacement = Settings.OutlinePlacement;
        _isSourceSoftWrap = Settings.IsSourceSoftWrap;
        _isPreviewSoftWrap = Settings.IsPreviewSoftWrap;
        _monoFontFamily = MonoFontStack.Build(Settings.MonoFont);
        _settingsService.Changed += OnSettingsChanged;

        Rendering.MarkdownRenderer.MonoFamily = new Avalonia.Media.FontFamily(_monoFontFamily);
        Rendering.MarkdownRenderer.Theme = Rendering.MarkdownThemes.Resolve(Settings.Theme);
        Rendering.MarkdownRenderer.WrapCode = Settings.IsPreviewSoftWrap;
        Rendering.MarkdownRenderer.BaseFontSize = Settings.FontSize;
        Rendering.MarkdownRenderer.MermaidScale = Settings.MermaidScale;
        Views.TextMateThemeResolver.Update(Settings.CodeTheme);
        RebuildOutline(_sourceText);
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? FindRequested;

    public event EventHandler? PreviewInvalidated;

    public event EventHandler? FindNextRequested;

    public event EventHandler? FindPreviousRequested;

    public AppSettings Settings { get; private set; }

    // Supplied by the view so the view-model can prompt before losing unsaved
    // work; null in headless tests, where guarded flows just proceed.
    public IDocumentInteraction? Interaction { get; set; }

    // The leading bullet flags unsaved edits in the window title.
    public string DisplayTitle => IsDirty ? $"• {DocumentTitle}" : DocumentTitle;

    public bool IsSourceOnly => CurrentViewMode is ViewMode.Source;

    public bool IsPreviewOnly => CurrentViewMode is ViewMode.Preview;

    public bool IsSplitVerticalActive => CurrentViewMode is ViewMode.SplitVertical;

    public bool IsSplitHorizontalActive => CurrentViewMode is ViewMode.SplitHorizontal;

    public bool IsDetachedActive => CurrentViewMode is ViewMode.Detached;

    // In detached mode the main window keeps showing the source (the preview
    // floats out into its own window). Source-only obviously stays visible too.
    public bool IsSourceVisibleInMain => CurrentViewMode is ViewMode.Source or ViewMode.Detached;

    // Cached so DocumentStats / ReadingMinutes do not re-scan the whole document
    // on every read; refreshed once per SourceText change.
    public int WordCount { get; private set; }

    public int CharCount => SourceText?.Length ?? 0;

    // 250 words/min is a conservative reading-speed default for prose; rounded up.
    public int ReadingMinutes => Math.Max(1, (int)Math.Ceiling(WordCount / 250.0));

    public string DocumentStats =>
        string.IsNullOrEmpty(LastModifiedText)
            ? $"{WordCount} words · {CharCount} chars · ~{ReadingMinutes} min"
            : $"{WordCount} words · {CharCount} chars · ~{ReadingMinutes} min · {LastModifiedText}";

    public bool IsOutlineLeftVisible => IsOutlineVisible && OutlinePlacement is OutlinePlacement.Left;

    public bool IsOutlineRightVisible => IsOutlineVisible && OutlinePlacement is OutlinePlacement.Right;

    public bool HasSelection => !string.IsNullOrEmpty(SelectionStats);

    public bool HasRecentFiles => Settings.RecentFiles.Count > 0;

    // Welcome view shows while the user has neither opened a file nor typed
    // into the placeholder. Disappears the moment a document is loaded or
    // scratch buffer is started.
    public bool IsWelcomeVisible =>
        string.IsNullOrEmpty(CurrentFilePath) && !IsScratchBuffer && !IsAwaitingInitialDocument;

    public async Task LoadFileAsync(string path, CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, App.ShutdownToken);
        var text = await File.ReadAllTextAsync(path, linked.Token);
        SetSourceTextFromDisk(text);
        CurrentFilePath = path;
        IsScratchBuffer = false;
        DocumentTitle = Path.GetFileName(path);
        LastModifiedText = FormatLastModified(path);
        StatusText = $"{DocumentTitle} • {text.Length:N0} chars";
        _fileWatcher.Watch(path);
        AddToRecent(path);
    }

    public async Task SaveToFileAsync(string path, CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, App.ShutdownToken);
        var written = SourceText;
        await File.WriteAllTextAsync(path, written, linked.Token);
        _lastSyncedText = written;
        CurrentFilePath = path;
        IsScratchBuffer = false;
        IsDirty = false;
        DocumentTitle = Path.GetFileName(path);
        LastModifiedText = FormatLastModified(path);
        StatusText = $"{DocumentTitle} • saved • {SourceText.Length:N0} chars";
        _fileWatcher.Watch(path);
        AddToRecent(path);
    }

    public void MoveHeading(OutlineNode dragged, OutlineNode? target, DropPosition position)
    {
        if (string.IsNullOrEmpty(SourceText))
        {
            return;
        }
        // -1 = "no target → append at end". Markdig's HeadingBlock.Line is
        // 0-indexed, so 0 is a real heading and can't be the sentinel.
        var targetLine = target?.SourceLine ?? -1;
        var updated = HeadingMover.Move(SourceText, OutlineNodes, dragged.SourceLine, targetLine, position);
        if (!string.Equals(updated, SourceText, StringComparison.Ordinal))
        {
            SourceText = updated;
            StatusText = $"Moved '{dragged.Text}'";
        }
    }

    public void PersistSession(int firstVisibleLine)
    {
        Settings.LastOpenedFile = CurrentFilePath;
        Settings.LastScrollLine = firstVisibleLine;
        _settingsService.Save(Settings);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settingsService.Changed -= OnSettingsChanged;
        _fileWatcher.FileChanged -= OnFileChangedOnBackgroundThread;
        _fileWatcher.Dispose();
        var cts = System.Threading.Interlocked.Exchange(ref _outlineCts, null);
        cts?.Cancel();
        cts?.Dispose();
    }

    // Reconciles the buffer with an on-disk change reported by the watcher.
    // Internal so it can be driven directly in tests without the real
    // FileSystemWatcher's timing.
    internal async Task HandleExternalChangeAsync(string path, WatcherChangeTypes change)
    {
        if (change == WatcherChangeTypes.Deleted)
        {
            StatusText = $"{DocumentTitle} • file deleted on disk";
            return;
        }

        string diskText;
        try
        {
            diskText = await File.ReadAllTextAsync(path, App.ShutdownToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (IOException)
        {
            // The writer may still hold the file mid-save; a later settle event
            // delivers the final content.
            return;
        }
        catch (UnauthorizedAccessException)
        {
            // Permissions changed under us; surface it instead of faulting the
            // fire-and-forget task that drives this reload.
            StatusText = $"{DocumentTitle} • permission denied on disk";
            return;
        }

        // Disk matches what we last wrote or loaded: this is our own save (or a
        // spurious event), not an external edit. Refresh metadata only so we
        // never reload over the user's position or prompt about our own write.
        if (string.Equals(diskText, _lastSyncedText, StringComparison.Ordinal))
        {
            CurrentFilePath = path;
            DocumentTitle = Path.GetFileName(path);
            LastModifiedText = FormatLastModified(path);
            return;
        }

        if (IsDirty && Interaction is not null && !await Interaction.ConfirmReloadAsync(DocumentTitle))
        {
            StatusText = $"{DocumentTitle} • changed on disk • keeping your edits";
            return;
        }

        CurrentFilePath = path;
        DocumentTitle = Path.GetFileName(path);
        SetSourceTextFromDisk(diskText);
        LastModifiedText = FormatLastModified(path);
        StatusText = $"{DocumentTitle} • reloaded • {diskText.Length:N0} chars";
    }

    private static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }
        var count = 0;
        var inWord = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                inWord = false;
                continue;
            }
            if (inWord)
            {
                continue;
            }
            inWord = true;
            count++;
        }
        return count;
    }

    private static void SetOutlineExpanded(IEnumerable<OutlineNode> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = expanded;
            SetOutlineExpanded(node.Children, expanded);
        }
    }

    private static bool ApplyOutlineFilter(IEnumerable<OutlineNode> nodes, string filter)
    {
        // Returns true if any node in the subtree matches; the parent stays
        // visible to expose the match path. Filter resets visibility when
        // empty so partial typing → erase restores the full tree.
        var anyMatch = false;
        foreach (var node in nodes)
        {
            var selfMatch = filter.Length == 0 || node.Text.Contains(filter, StringComparison.OrdinalIgnoreCase);
            var childMatch = ApplyOutlineFilter(node.Children, filter);
            node.IsVisible = selfMatch || childMatch;
            if (childMatch && filter.Length > 0)
            {
                node.IsExpanded = true;
            }
            anyMatch |= node.IsVisible;
        }
        return anyMatch;
    }

    private static string FormatLastModified(string path)
    {
        try
        {
            var lastWrite = File.GetLastWriteTime(path);
            return lastWrite.Date == DateTime.Today
                ? $"modified {lastWrite:HH:mm}"
                : $"modified {lastWrite:yyyy-MM-dd HH:mm}";
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    [RelayCommand(CanExecute = nameof(CanReload))]
    private async Task ReloadAsync()
    {
        if (CurrentFilePath is not { } path)
        {
            return;
        }

        if (IsDirty && Interaction is not null && !await Interaction.ConfirmReloadAsync(DocumentTitle))
        {
            return;
        }

        try
        {
            var text = await File.ReadAllTextAsync(path, App.ShutdownToken);
            SetSourceTextFromDisk(text);
            LastModifiedText = FormatLastModified(path);
            StatusText = $"{DocumentTitle} • reloaded • {text.Length:N0} chars";
        }
        catch (OperationCanceledException)
        {
            // Shutdown interrupted the reload.
        }
        catch (FileNotFoundException)
        {
            StatusText = $"{DocumentTitle} • file no longer exists";
        }
        catch (IOException ex)
        {
            StatusText = $"Reload failed: {ex.Message}";
        }
    }

    private bool CanReload()
    {
        return !string.IsNullOrEmpty(CurrentFilePath);
    }

    private void AddToRecent(string path)
    {
        const int maxRecent = 10;
        var list = Settings.RecentFiles;
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        while (list.Count > maxRecent)
        {
            list.RemoveAt(list.Count - 1);
        }
        _settingsService.Save(Settings);
    }

    [RelayCommand]
    private async Task OpenRecentAsync(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        if (!await EnsureSafeToDiscardAsync())
        {
            return;
        }
        try
        {
            await LoadFileAsync(path);
        }
        catch (OperationCanceledException)
        {
            // Shutdown interrupted the load.
        }
        catch (FileNotFoundException)
        {
            StatusText = $"{Path.GetFileName(path)} • file not found";
            // Match the case-insensitive dedup used when adding, so a stale
            // entry stored with different casing is still pruned.
            Settings.RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            _settingsService.Save(Settings);
        }
        catch (Exception ex)
        {
            StatusText = $"Open failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SetViewMode(ViewMode mode)
    {
        CurrentViewMode = mode;
    }

    [RelayCommand]
    private void SetThemeMode(string? mode)
    {
        if (string.IsNullOrEmpty(mode))
        {
            return;
        }
        Settings.ThemeMode = mode;
        _settingsService.Save(Settings);
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        if (!await EnsureSafeToDiscardAsync())
        {
            return;
        }
        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var path = CurrentFilePath;
        if (string.IsNullOrEmpty(path))
        {
            if (Interaction is null)
            {
                return;
            }
            path = await Interaction.PickSavePathAsync(SuggestedSaveName());
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
        }

        try
        {
            await SaveToFileAsync(path);
        }
        catch (OperationCanceledException)
        {
            // Shutdown interrupted the save.
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed. {ex.Message}";
        }
    }

    // Nothing meaningful to save while the welcome view is up (no file and no
    // scratch buffer); a document or scratch buffer enables Save.
    private bool CanSave()
    {
        return !IsWelcomeVisible;
    }

    private string SuggestedSaveName()
    {
        var name = DocumentTitle;
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "Markus", StringComparison.Ordinal))
        {
            return "Untitled.md";
        }
        return string.IsNullOrEmpty(Path.GetExtension(name)) ? $"{name}.md" : name;
    }

    // Gate before any action that replaces the buffer (open, new, switch). A
    // clean document, or no view to prompt with, proceeds without asking.
    private async Task<bool> EnsureSafeToDiscardAsync()
    {
        if (!IsDirty || Interaction is null)
        {
            return true;
        }
        switch (await Interaction.ConfirmDiscardAsync(DocumentTitle))
        {
            case UnsavedChangesChoice.Save:
                await SaveAsync();
                // A cancelled Save-As leaves the document dirty: abort the
                // discard rather than silently dropping the edits.
                return !IsDirty;
            case UnsavedChangesChoice.Discard:
                return true;
            default:
                return false;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ToggleOutline()
    {
        IsOutlineVisible = !IsOutlineVisible;
    }

    [RelayCommand]
    private void ToggleScrollLock()
    {
        IsScrollLocked = !IsScrollLocked;
    }

    [RelayCommand]
    private void ToggleSourceSoftWrap()
    {
        IsSourceSoftWrap = !IsSourceSoftWrap;
    }

    [RelayCommand]
    private void TogglePreviewSoftWrap()
    {
        IsPreviewSoftWrap = !IsPreviewSoftWrap;
    }

    [RelayCommand]
    private void ToggleFocusMode()
    {
        IsFocusMode = !IsFocusMode;
    }

    [RelayCommand]
    private void ToggleTypewriterMode()
    {
        IsTypewriterMode = !IsTypewriterMode;
    }

    [RelayCommand]
    private void Find()
    {
        FindRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void FindNext()
    {
        FindNextRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void FindPrevious()
    {
        FindPreviousRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task NewScratchAsync()
    {
        if (!await EnsureSafeToDiscardAsync())
        {
            return;
        }
        // A scratch buffer has no backing file: drop the previous path and its
        // watcher so a later Save prompts for a destination and an external
        // change to the old file can't reload over the scratch.
        _fileWatcher.Stop();
        CurrentFilePath = null;
        SetSourceTextFromDisk(string.Empty);
        IsScratchBuffer = true;
        DocumentTitle = "Untitled";
        LastModifiedText = string.Empty;
        StatusText = "Scratch buffer · unsaved";
    }

    [RelayCommand]
    private void FormatTables()
    {
        var current = SourceText;
        if (string.IsNullOrEmpty(current))
        {
            return;
        }
        var formatted = MarkdownTableFormatter.Format(current);
        if (!string.Equals(formatted, current, StringComparison.Ordinal))
        {
            SourceText = formatted;
            StatusText = "Tables reformatted";
        }
    }

    [RelayCommand]
    private void ExpandOutline()
    {
        SetOutlineExpanded(OutlineNodes, true);
    }

    [RelayCommand]
    private void CollapseOutline()
    {
        SetOutlineExpanded(OutlineNodes, false);
    }

    partial void OnOutlineFilterChanged(string value)
    {
        ApplyOutlineFilter(OutlineNodes, value?.Trim() ?? string.Empty);
    }

    partial void OnOutlineNodesChanged(IReadOnlyList<OutlineNode> value)
    {
        // Re-apply the current filter so a freshly-parsed outline doesn't
        // discard the user's quick-search state.
        if (!string.IsNullOrEmpty(OutlineFilter))
        {
            ApplyOutlineFilter(value, OutlineFilter.Trim());
        }
    }

    partial void OnOutlinePlacementChanged(OutlinePlacement value)
    {
        if (_suppressOutlinePlacementSave)
        {
            return;
        }
        Settings.OutlinePlacement = value;
        _settingsService.Save(Settings);
    }

    partial void OnIsOutlineVisibleChanged(bool value)
    {
        if (_suppressOutlineVisibleSave)
        {
            return;
        }
        Settings.ShowOutline = value;
        _settingsService.Save(Settings);
    }

    partial void OnIsSourceSoftWrapChanged(bool value)
    {
        Settings.IsSourceSoftWrap = value;
        _settingsService.Save(Settings);
    }

    partial void OnIsPreviewSoftWrapChanged(bool value)
    {
        Settings.IsPreviewSoftWrap = value;
        Rendering.MarkdownRenderer.WrapCode = value;
        _settingsService.Save(Settings);
        PreviewInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        Settings = e.Settings;
        ThemeApplicator.Apply(e.Settings.ThemeMode);
        SyncOutlineVisible(e.Settings);
        SyncOutlinePlacement(e.Settings);
        SyncEditorFlags(e.Settings);

        if (ApplyRendererSettings(e.Settings))
        {
            PreviewInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SyncOutlineVisible(AppSettings s)
    {
        if (s.ShowOutline == IsOutlineVisible)
        {
            return;
        }

        // Apply the persisted flag to the live window without echoing it back to
        // disk through OnIsOutlineVisibleChanged.
        _suppressOutlineVisibleSave = true;
        try
        {
            IsOutlineVisible = s.ShowOutline;
        }
        finally
        {
            _suppressOutlineVisibleSave = false;
        }
    }

    private void SyncOutlinePlacement(AppSettings s)
    {
        if (s.OutlinePlacement == OutlinePlacement)
        {
            return;
        }

        _suppressOutlinePlacementSave = true;
        try
        {
            OutlinePlacement = s.OutlinePlacement;
        }
        finally
        {
            _suppressOutlinePlacementSave = false;
        }
    }

    private void SyncEditorFlags(AppSettings s)
    {
        if (IsSourceSoftWrap != s.IsSourceSoftWrap)
        {
            IsSourceSoftWrap = s.IsSourceSoftWrap;
        }

        if (IsPreviewSoftWrap != s.IsPreviewSoftWrap)
        {
            IsPreviewSoftWrap = s.IsPreviewSoftWrap;
        }
    }

    private bool ApplyRendererSettings(AppSettings s)
    {
        var fontStack = MonoFontStack.Build(s.MonoFont);
        var fontChanged = !string.Equals(fontStack, MonoFontFamily, StringComparison.Ordinal);
        var newTheme = Rendering.MarkdownThemes.Resolve(s.Theme);
        var themeChanged = !ReferenceEquals(newTheme, Rendering.MarkdownRenderer.Theme);
        var fontSizeChanged = Math.Abs(Rendering.MarkdownRenderer.BaseFontSize - s.FontSize) > 0.01;
        var mermaidChanged = Math.Abs(Rendering.MarkdownRenderer.MermaidScale - s.MermaidScale) > 0.01;

        if (fontChanged)
        {
            MonoFontFamily = fontStack;
            Rendering.MarkdownRenderer.MonoFamily = new Avalonia.Media.FontFamily(fontStack);
        }

        if (themeChanged)
        {
            Rendering.MarkdownRenderer.Theme = newTheme;
        }

        Rendering.MarkdownRenderer.BaseFontSize = s.FontSize;
        Rendering.MarkdownRenderer.MermaidScale = s.MermaidScale;
        Views.TextMateThemeResolver.Update(s.CodeTheme);

        return fontChanged || themeChanged || fontSizeChanged || mermaidChanged;
    }

    partial void OnSourceTextChanged(string value)
    {
        // Refresh the cached count before the generated WordCount/DocumentStats
        // change notifications fire, so the status bar reads it without re-scanning.
        WordCount = CountWords(value);
        if (!_loadingDocument)
        {
            IsDirty = true;
        }
        _ = RebuildOutlineAsync(value);
    }

    // Replaces the buffer with content that mirrors disk (open/reload), so the
    // document is clean afterwards rather than flagged as a user edit.
    private void SetSourceTextFromDisk(string text)
    {
        _loadingDocument = true;
        try
        {
            SourceText = text;
        }
        finally
        {
            _loadingDocument = false;
        }
        _lastSyncedText = text;
        IsDirty = false;
    }

    private async System.Threading.Tasks.Task RebuildOutlineAsync(string source)
    {
        // Swap atomically so two concurrent fire-and-forget calls never
        // race on the same CTS reference.
        var fresh = CancellationTokenSource.CreateLinkedTokenSource(App.ShutdownToken);
        var previous = System.Threading.Interlocked.Exchange(ref _outlineCts, fresh);
        if (previous is not null)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }
        var token = fresh.Token;
        try
        {
            // Debounce: a newer keystroke cancels this token before the delay
            // elapses, so the document is parsed only after typing settles.
            await System.Threading.Tasks.Task.Delay(OutlineDebounceMs, token);
            // Parse + tree-build on the threadpool; the result hop back to the
            // UI thread via the captured sync context for the property assign.
            var nodes = await System.Threading.Tasks.Task.Run(
                () =>
                {
                    var document = MarkdownPipeline.Parse(source);
                    return OutlineBuilder.Build(document);
                },
                token
            );
            if (!token.IsCancellationRequested)
            {
                OutlineNodes = nodes;
            }
        }
        catch (OperationCanceledException)
        {
            // Newer text already in flight; let it win.
        }
        catch (Exception)
        {
            OutlineNodes = Array.Empty<OutlineNode>();
        }
    }

    private void RebuildOutline(string source)
    {
        // Synchronous fallback for the constructor's first invocation, since
        // we can't await before the window is wired up. Still off-thread? No,
        // but the initial welcome text is tiny so the cost is negligible.
        try
        {
            var document = MarkdownPipeline.Parse(source);
            OutlineNodes = OutlineBuilder.Build(document);
        }
        catch (Exception)
        {
            OutlineNodes = Array.Empty<OutlineNode>();
        }
    }

    private void OnFileChangedOnBackgroundThread(object? sender, FileChangedEventArgs e)
    {
        // FileSystemWatcher fires on a thread-pool thread; hop to UI before
        // touching observable state.
        Dispatcher.UIThread.Post(() => _ = HandleExternalChangeAsync(e.Path, e.Change));
    }
}
