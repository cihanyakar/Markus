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
    private string _statusText = "No file open";

    [ObservableProperty]
    private string _documentTitle = "Markus";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReloadCommand))]
    private string? _currentFilePath;

    [ObservableProperty]
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
        _monoFontFamily = MonoFontStack.Build(Settings.MonoFont);
        _settingsService.Changed += OnSettingsChanged;

        Rendering.MarkdownRenderer.MonoFamily = new Avalonia.Media.FontFamily(_monoFontFamily);
        Rendering.MarkdownRenderer.Theme = Rendering.MarkdownThemes.Resolve(Settings.Theme);
        Views.TextMateThemeResolver.Update(Settings.CodeTheme);
        RebuildOutline(_sourceText);
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? SettingsRequested;

    public AppSettings Settings { get; private set; }

    public bool IsSourceOnly => CurrentViewMode is ViewMode.Source;

    public bool IsPreviewOnly => CurrentViewMode is ViewMode.Preview;

    public bool IsSplitVerticalActive => CurrentViewMode is ViewMode.SplitVertical;

    public bool IsSplitHorizontalActive => CurrentViewMode is ViewMode.SplitHorizontal;

    // In detached mode the main window keeps showing the source (the preview
    // floats out into its own window). Source-only obviously stays visible too.
    public bool IsSourceVisibleInMain => CurrentViewMode is ViewMode.Source or ViewMode.Detached;

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
        RebuildOutline(value);
    }

    private void RebuildOutline(string source)
    {
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
