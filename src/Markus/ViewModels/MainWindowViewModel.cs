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
    [NotifyPropertyChangedFor(nameof(IsDetached))]
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
        + "- Live reload (active once a file is open)\n"
        + "- Native preview renderer (next iteration)\n"
        + "- Multiple themes\n";

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
        _settingsService.Changed += OnSettingsChanged;
    }

    public AppSettings Settings { get; private set; }

    public bool IsSourceOnly => CurrentViewMode is ViewMode.Source;

    public bool IsPreviewOnly => CurrentViewMode is ViewMode.Preview;

    public bool IsSplitVerticalActive => CurrentViewMode is ViewMode.SplitVertical;

    public bool IsSplitHorizontalActive => CurrentViewMode is ViewMode.SplitHorizontal;

    public bool IsDetached => CurrentViewMode is ViewMode.Detached;

    public async Task LoadFileAsync(string path)
    {
        var text = await File.ReadAllTextAsync(path);
        SourceText = text;
        CurrentFilePath = path;
        DocumentTitle = Path.GetFileName(path);
        StatusText = $"{DocumentTitle} • {text.Length} chars";
        _fileWatcher.Watch(path);
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

    [RelayCommand]
    private void SetViewMode(ViewMode mode)
    {
        CurrentViewMode = mode;
    }

    [RelayCommand]
    private void ToggleOutline()
    {
        IsOutlineVisible = !IsOutlineVisible;
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        Settings = e.Settings;
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
