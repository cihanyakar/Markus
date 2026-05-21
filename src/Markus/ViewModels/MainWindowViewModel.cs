using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Markus.Models;
using Markus.Services;

namespace Markus.ViewModels;

internal sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly FileWatcherService _fileWatcher;
    private System.Threading.CancellationTokenSource? _outlineCts;
    private bool _disposed;
    private bool _suppressOutlinePlacementSave;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSourceOnly))]
    [NotifyPropertyChangedFor(nameof(IsPreviewOnly))]
    [NotifyPropertyChangedFor(nameof(IsSplitVerticalActive))]
    [NotifyPropertyChangedFor(nameof(IsSplitHorizontalActive))]
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
    private string _documentTitle = "Markus";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReloadCommand))]
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
    [NotifyPropertyChangedFor(nameof(IsWelcomeVisible))]
    private bool _isScratchBuffer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DocumentStats))]
    private string _lastModifiedText = string.Empty;

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

    public bool IsSourceOnly => CurrentViewMode is ViewMode.Source;

    public bool IsPreviewOnly => CurrentViewMode is ViewMode.Preview;

    public bool IsSplitVerticalActive => CurrentViewMode is ViewMode.SplitVertical;

    public bool IsSplitHorizontalActive => CurrentViewMode is ViewMode.SplitHorizontal;

    // In detached mode the main window keeps showing the source (the preview
    // floats out into its own window). Source-only obviously stays visible too.
    public bool IsSourceVisibleInMain => CurrentViewMode is ViewMode.Source or ViewMode.Detached;

    public int WordCount => CountWords(SourceText);

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
    public bool IsWelcomeVisible => string.IsNullOrEmpty(CurrentFilePath) && !IsScratchBuffer;

    public async Task LoadFileAsync(string path, CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, App.ShutdownToken);
        var text = await File.ReadAllTextAsync(path, linked.Token);
        SourceText = text;
        CurrentFilePath = path;
        IsScratchBuffer = false;
        DocumentTitle = Path.GetFileName(path);
        LastModifiedText = FormatLastModified(path);
        StatusText = $"{DocumentTitle} • {text.Length:N0} chars";
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
        _outlineCts?.Dispose();
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

        try
        {
            var text = await File.ReadAllTextAsync(path, App.ShutdownToken);
            SourceText = text;
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
            Settings.RecentFiles.Remove(path);
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
    private void OpenFile()
    {
        OpenRequested?.Invoke(this, EventArgs.Empty);
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
    private void NewScratch()
    {
        SourceText = string.Empty;
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
        SyncOutlinePlacement(e.Settings);
        SyncEditorFlags(e.Settings);

        if (ApplyRendererSettings(e.Settings))
        {
            PreviewInvalidated?.Invoke(this, EventArgs.Empty);
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
        _ = RebuildOutlineAsync(value);
    }

    private async System.Threading.Tasks.Task RebuildOutlineAsync(string source)
    {
        // Cancel the in-flight outline so rapid typing doesn't stack parses.
        if (_outlineCts is { } previous)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }
        _outlineCts = CancellationTokenSource.CreateLinkedTokenSource(App.ShutdownToken);
        var token = _outlineCts.Token;
        try
        {
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
        Dispatcher.UIThread.Post(() =>
        {
            if (e.Change == WatcherChangeTypes.Deleted)
            {
                StatusText = $"{DocumentTitle} • file deleted on disk";
                return;
            }

            CurrentFilePath = e.Path;
            DocumentTitle = Path.GetFileName(e.Path);
            ReloadCommand.Execute(null);
        });
    }
}
