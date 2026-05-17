using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Markus.Services;
using Markus.ViewModels;
using Markus.Views.Platform;

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

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        DragDrop.SetAllowDrop(this, true);

        ConfigureExtendedTitleBar();
        Opened += OnWindowOpened;
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
        // Only start a window drag when the user grabs an empty toolbar area;
        // buttons and combos must keep their own click semantics.
        if (e.Source is Visual source && IsInteractiveChild(source))
        {
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
        }
        else if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.DocumentTitle), StringComparison.Ordinal))
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
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }
        if (vm.CurrentViewMode == Markus.Models.ViewMode.Preview)
        {
            PreviewOnlyView.ScrollToLine(sourceLine);
        }
        else if (vm.CurrentViewMode == Markus.Models.ViewMode.SplitVertical)
        {
            SplitVPreviewView.ScrollToLine(sourceLine);
        }
        else if (vm.CurrentViewMode == Markus.Models.ViewMode.SplitHorizontal)
        {
            SplitHPreviewView.ScrollToLine(sourceLine);
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
