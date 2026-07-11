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

    // Files above this size build the whole preview/outline tree synchronously,
    // which freezes the UI on open. Beyond the threshold we warn the user and
    // load the raw text without eagerly forcing the heavy render path. 8 MiB is
    // well past any hand-written markdown yet small enough that the warning
    // never fires for ordinary documents.
    private const long LargeFileWarningBytes = 8L * 1024 * 1024;

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
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
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
    private double _editorFontSize;

    [ObservableProperty]
    private bool _showLineNumbers;

    [ObservableProperty]
    private bool _highlightCurrentLine;

    [ObservableProperty]
    private bool _autoPairBrackets;

    [ObservableProperty]
    private int _tabWidth;

    [ObservableProperty]
    private bool _previewFullWidth;

    [ObservableProperty]
    private bool _previewLeftAligned;

    [ObservableProperty]
    private string _caretPosition = "Ln 1, Col 1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private string _selectionStats = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
    [NotifyPropertyChangedFor(nameof(IsWelcomeVisible))]
    private bool _isScratchBuffer;

    // True for a brief grace period at launch on macOS, where a double-clicked
    // document is delivered via an AppleEvent that lands just after the window
    // is shown. Suppressing the welcome screen during that window keeps the
    // incoming document from flashing past an empty welcome render.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcomeVisible))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
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
        _editorFontSize = Settings.EditorFontSize;
        _showLineNumbers = Settings.ShowLineNumbers;
        _highlightCurrentLine = Settings.HighlightCurrentLine;
        _autoPairBrackets = Settings.AutoPairBrackets;
        _tabWidth = Settings.TabWidth;
        _previewFullWidth = Settings.PreviewFullWidth;
        _previewLeftAligned = Settings.PreviewLeftAligned;
        _settingsService.Changed += OnSettingsChanged;

        Rendering.MarkdownRenderer.MonoFamily = new Avalonia.Media.FontFamily(_monoFontFamily);
        Rendering.MarkdownRenderer.Theme = Rendering.MarkdownThemes.Resolve(Settings.Theme);
        Rendering.MarkdownRenderer.WrapCode = Settings.IsPreviewSoftWrap;
        Rendering.MarkdownRenderer.BaseFontSize = Settings.FontSize;
        Rendering.MarkdownRenderer.MermaidScale = Settings.MermaidScale;
        Rendering.MarkdownRenderer.EnableMath = Settings.EnableMath;
        Rendering.MarkdownRenderer.EnableMermaid = Settings.EnableMermaid;
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
        // A very large file forces a synchronous parse of the whole preview and
        // outline tree, which freezes the UI. Probe the on-disk size first so we
        // can warn the user rather than lock up mid-open. The read still
        // proceeds (the buffer stays raw text), so no document is lost.
        var isLarge = false;
        try
        {
            isLarge = IsFileTooLargeToOpen(new FileInfo(path).Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Size probe failed (permissions, transient IO). Fall through and let
            // the read below surface any real error.
        }
        var text = await File.ReadAllTextAsync(path, linked.Token);
        SetSourceTextFromDisk(text);
        CurrentFilePath = path;
        IsScratchBuffer = false;
        DocumentTitle = Path.GetFileName(path);
        LastModifiedText = FormatLastModified(path);
        StatusText = isLarge
            ? $"{DocumentTitle} • {text.Length:N0} chars • large file, preview may be slow"
            : $"{DocumentTitle} • {text.Length:N0} chars";
        _fileWatcher.Watch(path);
        AddToRecent(path);
    }

    public async Task SaveToFileAsync(string path, CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, App.ShutdownToken);
        var written = SourceText;
        // Atomic write (temp + fsync + rename) so a crash, power loss, or a
        // full disk mid-save cannot truncate or corrupt the user's existing
        // file. AtomicFileWriter is synchronous; run it off the UI thread to
        // keep the async contract and avoid blocking on the fsync.
        await Task.Run(() => AtomicFileWriter.WriteAllText(path, written), linked.Token);
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
        // Flush any debounced slider save before stamping the session state
        // so we do not lose a slider value mid-drag at exit (the trailing
        // debounce timer would never fire after the app exits).
        _settingsService.FlushPendingSave();
        Settings.LastOpenedFile = CurrentFilePath;
        Settings.LastScrollLine = firstVisibleLine;
        _settingsService.Save(Settings);
    }

    // Best-effort crash recovery. Writes the current unsaved buffer to a
    // recovery file so a global exception handler can preserve the user's work
    // before the process dies. Returns true if a recovery copy was written.
    // Never throws, since a crash handler calls this while the app is unwinding.
    public bool TryDumpRecovery(string recoveryDirectory)
    {
        try
        {
            // Nothing worth preserving when the buffer is clean or empty.
            if (!IsDirty)
            {
                return false;
            }
            var text = SourceText;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            Directory.CreateDirectory(recoveryDirectory);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var baseName = string.IsNullOrEmpty(CurrentFilePath)
                ? "untitled"
                : Path.GetFileNameWithoutExtension(CurrentFilePath);
            var recoveryPath = Path.Combine(recoveryDirectory, $"{baseName}.{stamp}.recovered.md");
            File.WriteAllText(recoveryPath, text);
            return true;
        }
        catch (Exception)
        {
            // A crash handler must never fault. Swallow every failure.
            return false;
        }
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

    // A file at or above the warning threshold is treated as too large to open
    // through the normal eager-render path. Pulled out as a pure decision so the
    // threshold logic can be unit-tested without touching the filesystem.
    internal static bool IsFileTooLargeToOpen(long lengthBytes)
    {
        return lengthBytes >= LargeFileWarningBytes;
    }

    // True when a watcher event's path refers to the file the watcher is
    // currently tracking. Debounce plus the UI-thread dispatch hop means an
    // event posted for the previous file can arrive after the user has already
    // opened a different one; without this check a stale event would overwrite
    // the new document with the old file's content. Comparing against the live
    // WatchedPath (not CurrentFilePath) keeps an external rename followed, since
    // the watcher advances WatchedPath to the renamed file. Uses the same
    // canonicalize + case-insensitive comparison the open-router uses for its
    // own path matching.
    internal static bool PathMatchesWatched(string? watchedPath, string eventPath)
    {
        if (string.IsNullOrEmpty(watchedPath) || string.IsNullOrEmpty(eventPath))
        {
            return false;
        }
        return string.Equals(CanonicalPath(watchedPath), CanonicalPath(eventPath), StringComparison.OrdinalIgnoreCase);
    }

    // Guarded buffer-replacing load: prompts to save unsaved work first, then
    // loads the file. Returns false if the user cancelled.
    internal async Task<bool> LoadFileGuardedAsync(string path)
    {
        if (!await EnsureSafeToDiscardAsync())
        {
            return false;
        }
        await LoadFileAsync(path);
        return true;
    }

    // Reconciles the buffer with an on-disk change reported by the watcher.
    // Internal so it can be driven directly in tests without the real
    // FileSystemWatcher's timing.
    internal async Task HandleExternalChangeAsync(string path, WatcherChangeTypes change)
    {
        // Runs on the UI thread. A debounced event can still be posted to the UI
        // queue for a file the user has since closed (they opened a different
        // document while it was in flight) and would otherwise clobber the new
        // buffer. Compare against the watcher's live target rather than
        // CurrentFilePath, so a genuine external rename (which advances
        // WatchedPath to the new name while CurrentFilePath still holds the old
        // one) is still followed, while a stale event for a no-longer-watched
        // file is dropped, for the reload and the delete branch alike.
        if (!PathMatchesWatched(_fileWatcher.WatchedPath, path))
        {
            return;
        }

        if (change == WatcherChangeTypes.Deleted)
        {
            HandleExternalDeletion();
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

        // Disk matches what we last wrote or loaded, so this is our own save (or
        // a spurious event), not an external edit. Refresh metadata only so we
        // never reload over the user's position or prompt about our own write.
        if (string.Equals(diskText, _lastSyncedText, StringComparison.Ordinal))
        {
            RefreshMetadataAfterOwnWrite(path);
            return;
        }

        if (IsDirty && Interaction is not null && !await Interaction.ConfirmReloadAsync(DocumentTitle))
        {
            StatusText = $"{DocumentTitle} • changed on disk • keeping your edits";
            return;
        }

        ApplyExternalReload(path, diskText);
    }

    private static string CanonicalPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
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

    // The on-disk file vanished while open, so the buffer is now the only copy
    // and is unsaved relative to disk. Flag it dirty so the close guard warns on
    // quit and clear the stale modified stamp. Keep CurrentFilePath so a later
    // Save recreates the file at its original location.
    private void HandleExternalDeletion()
    {
        IsDirty = true;
        LastModifiedText = string.Empty;
        StatusText = $"{DocumentTitle} • file deleted on disk";
    }

    // Refreshes metadata after recognizing an on-disk change as our own save (or
    // a spurious event), without reloading over the user's cursor position.
    private void RefreshMetadataAfterOwnWrite(string path)
    {
        CurrentFilePath = path;
        DocumentTitle = Path.GetFileName(path);
        LastModifiedText = FormatLastModified(path);
    }

    // Replaces the buffer with an external edit read from disk and refreshes
    // the document metadata to match.
    private void ApplyExternalReload(string path, string diskText)
    {
        CurrentFilePath = path;
        DocumentTitle = Path.GetFileName(path);
        SetSourceTextFromDisk(diskText);
        LastModifiedText = FormatLastModified(path);
        StatusText = $"{DocumentTitle} • reloaded • {diskText.Length:N0} chars";
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
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // A moved or deleted parent directory throws DirectoryNotFoundException
            // (an IOException, not FileNotFoundException), so treat both as a
            // missing file and prune the dead entry.
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
        catch (OperationCanceledException) when (App.ShutdownToken.IsCancellationRequested)
        {
            // Genuine shutdown interrupted the save; nothing more we can do.
        }
        catch (OperationCanceledException)
        {
            // Cancelled for some other reason (not a real process shutdown), so
            // the edits are still unsaved. Surface it instead of silently no-oping.
            StatusText = "Save cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed. {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsAsync()
    {
        // Always pick a fresh destination so the user can fork or retarget an
        // already-saved document; SaveToFileAsync then repoints CurrentFilePath.
        if (Interaction is null)
        {
            return;
        }
        var path = await Interaction.PickSavePathAsync(SuggestedSaveName());
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        try
        {
            await SaveToFileAsync(path);
        }
        catch (OperationCanceledException) when (App.ShutdownToken.IsCancellationRequested)
        {
            // Genuine shutdown interrupted the save.
        }
        catch (OperationCanceledException)
        {
            StatusText = "Save cancelled";
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
    private void TogglePreviewLeftAligned()
    {
        PreviewLeftAligned = !PreviewLeftAligned;
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
        // A fresh scratch buffer is empty, so a preview-only view would land the
        // user on a blank pane with nowhere to type. Drop to the source editor.
        // Split and detached views already show the source, so leave them alone.
        if (CurrentViewMode is ViewMode.Preview)
        {
            SetViewMode(ViewMode.Source);
        }
    }

    [RelayCommand]
    private void FormatTables()
    {
        var current = SourceText;
        if (string.IsNullOrEmpty(current))
        {
            StatusText = "No tables to format";
            return;
        }
        var formatted = MarkdownTableFormatter.Format(current);
        if (!string.Equals(formatted, current, StringComparison.Ordinal))
        {
            SourceText = formatted;
            StatusText = "Tables reformatted";
            return;
        }
        // Format made no change: either there are no tables, or every table is
        // already aligned. Either way the command did run, so say so instead of
        // looking dead.
        StatusText = "Tables already formatted";
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

    partial void OnPreviewLeftAlignedChanged(bool value)
    {
        Settings.PreviewLeftAligned = value;
        _settingsService.Save(Settings);
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        // The debounced save timer raises Changed on a thread-pool thread.
        // This handler mutates UI-bound state, re-applies the theme, and
        // rebuilds the preview, so hop to the UI thread when we are not
        // already on it. The Application.Current guard mirrors ThemeApplicator
        // so headless tests (no Avalonia app) keep running synchronously.
        if (Avalonia.Application.Current is not null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnSettingsChanged(sender, e));
            return;
        }
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
        var renderTogglesChanged =
            Rendering.MarkdownRenderer.EnableMath != s.EnableMath
            || Rendering.MarkdownRenderer.EnableMermaid != s.EnableMermaid;

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

        Rendering.MarkdownRenderer.EnableMath = s.EnableMath;
        Rendering.MarkdownRenderer.EnableMermaid = s.EnableMermaid;

        EditorFontSize = s.EditorFontSize;
        ShowLineNumbers = s.ShowLineNumbers;
        HighlightCurrentLine = s.HighlightCurrentLine;
        AutoPairBrackets = s.AutoPairBrackets;
        TabWidth = s.TabWidth;
        PreviewFullWidth = s.PreviewFullWidth;
        PreviewLeftAligned = s.PreviewLeftAligned;

        return fontChanged || themeChanged || fontSizeChanged || mermaidChanged || renderTogglesChanged;
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
        Dispatcher.UIThread.Post(() => _ = HandleExternalChangeSafeAsync(e.Path, e.Change));
    }

    // Guards the fire-and-forget reload so a failure in the prompt or disk read
    // surfaces as status text instead of an unobserved task exception.
    private async Task HandleExternalChangeSafeAsync(string path, WatcherChangeTypes change)
    {
        try
        {
            await HandleExternalChangeAsync(path, change);
        }
        catch (Exception ex)
        {
            StatusText = $"Could not handle external change • {ex.Message}";
        }
    }
}
