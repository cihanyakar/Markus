using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Markus.Services;
using Markus.ViewModels;

namespace Markus.Views;

internal sealed partial class MainWindow : Window
{
    private DetachedSourceWindow? _sourceWindow;
    private DetachedPreviewWindow? _previewWindow;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnWindowClosing;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateDetachedWindows(vm);
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
        }
    }

    private void UpdateDetachedWindows(MainWindowViewModel vm)
    {
        if (vm.CurrentViewMode == Markus.Models.ViewMode.Detached)
        {
            OpenDetachedWindows(vm);
        }
        else
        {
            CloseDetachedWindows();
        }
    }

    private void OpenDetachedWindows(MainWindowViewModel vm)
    {
        if (_sourceWindow is null)
        {
            _sourceWindow = new DetachedSourceWindow { DataContext = vm };
            _sourceWindow.Closed += (_, _) => _sourceWindow = null;
            _sourceWindow.Show(this);
        }
        if (_previewWindow is null)
        {
            _previewWindow = new DetachedPreviewWindow { DataContext = vm };
            _previewWindow.Closed += (_, _) => _previewWindow = null;
            _previewWindow.Show(this);
        }
    }

    private void CloseDetachedWindows()
    {
        _sourceWindow?.Close();
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
            vm.Dispose();
        }
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
