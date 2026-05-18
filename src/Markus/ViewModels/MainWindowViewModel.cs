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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSourceOnly))]
    [NotifyPropertyChangedFor(nameof(IsPreviewOnly))]
    [NotifyPropertyChangedFor(nameof(IsSplitVerticalActive))]
    [NotifyPropertyChangedFor(nameof(IsSplitHorizontalActive))]
    [NotifyPropertyChangedFor(nameof(IsSourceVisibleInMain))]
    private ViewMode _currentViewMode;

    [ObservableProperty]
    private bool _isOutlineVisible;

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
    private string? _currentFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WordCount))]
    [NotifyPropertyChangedFor(nameof(CharCount))]
    [NotifyPropertyChangedFor(nameof(ReadingMinutes))]
    [NotifyPropertyChangedFor(nameof(DocumentStats))]
    private string _sourceText =
        "# Welcome to Markus\n\n"
        + "This is a **placeholder**. Open a `.md` file to see it rendered.\n\n"
        + "## Features\n\n"
        + "- Live reload (active once a file is open)\n"
        + "- Native preview renderer\n"
        + "- Multiple themes\n\n"
        + "## Coming next\n\n"
        + "- Detached source/preview windows\n"
        + "- Theme tokens applied to the rendered content\n";

    [ObservableProperty]
    private IReadOnlyList<OutlineNode> _outlineNodes = Array.Empty<OutlineNode>();

    [ObservableProperty]
    private string _monoFontFamily;

    [ObservableProperty]
    private string _caretPosition = "Ln 1, Col 1";

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
        _isSourceSoftWrap = Settings.IsSourceSoftWrap;
        _isPreviewSoftWrap = Settings.IsPreviewSoftWrap;
        _monoFontFamily = MonoFontStack.Build(Settings.MonoFont);
        _settingsService.Changed += OnSettingsChanged;

        Rendering.MarkdownRenderer.MonoFamily = new Avalonia.Media.FontFamily(_monoFontFamily);
        Rendering.MarkdownRenderer.Theme = Rendering.MarkdownThemes.Resolve(Settings.Theme);
        Rendering.MarkdownRenderer.WrapCode = Settings.IsPreviewSoftWrap;
        Views.TextMateThemeResolver.Update(Settings.CodeTheme);
        RebuildOutline(_sourceText);
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? FindRequested;

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

    public string DocumentStats => $"{WordCount} words · {CharCount} chars · ~{ReadingMinutes} min";

    public async Task LoadFileAsync(string path)
    {
        var text = await File.ReadAllTextAsync(path);
        SourceText = text;
        CurrentFilePath = path;
        DocumentTitle = Path.GetFileName(path);
        StatusText = $"{DocumentTitle} • {text.Length} chars";
        _fileWatcher.Watch(path);
        AddToRecent(path);
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

    [RelayCommand(CanExecute = nameof(CanReload))]
    private async Task ReloadAsync()
    {
        if (CurrentFilePath is not { } path)
        {
            return;
        }

        try
        {
            var text = await File.ReadAllTextAsync(path);
            SourceText = text;
            StatusText = $"{DocumentTitle} • reloaded • {text.Length} chars";
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
        // Force the preview re-render so already-rendered code blocks pick up
        // the new wrap mode immediately.
        var current = SourceText;
        SourceText = string.Empty;
        SourceText = current;
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        Settings = e.Settings;
        ThemeApplicator.Apply(e.Settings.ThemeMode);

        var fontStack = MonoFontStack.Build(e.Settings.MonoFont);
        var fontChanged = !string.Equals(fontStack, MonoFontFamily, StringComparison.Ordinal);

        var newTheme = Rendering.MarkdownThemes.Resolve(e.Settings.Theme);
        var themeChanged = !ReferenceEquals(newTheme, Rendering.MarkdownRenderer.Theme);

        if (fontChanged)
        {
            MonoFontFamily = fontStack;
            Rendering.MarkdownRenderer.MonoFamily = new Avalonia.Media.FontFamily(fontStack);
        }

        if (themeChanged)
        {
            Rendering.MarkdownRenderer.Theme = newTheme;
        }

        // Code (TextMate) theme is independent from the preview theme.
        Views.TextMateThemeResolver.Update(e.Settings.CodeTheme);

        if (IsSourceSoftWrap != e.Settings.IsSourceSoftWrap)
        {
            IsSourceSoftWrap = e.Settings.IsSourceSoftWrap;
        }

        if (IsPreviewSoftWrap != e.Settings.IsPreviewSoftWrap)
        {
            IsPreviewSoftWrap = e.Settings.IsPreviewSoftWrap;
        }

        if (fontChanged || themeChanged)
        {
            // Nudge SourceText so preview re-renders with the new look.
            var current = SourceText;
            SourceText = string.Empty;
            SourceText = current;
        }
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
        _outlineCts = new System.Threading.CancellationTokenSource();
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
