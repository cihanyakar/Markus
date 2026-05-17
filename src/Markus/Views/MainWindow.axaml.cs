using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Markus.Models;
using Markus.Services;
using Markus.ViewModels;

namespace Markus.Views;

internal sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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

    private void Reload_Click(object? sender, RoutedEventArgs e)
    {
        SetStatus("Reload — not yet wired to a file source");
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

        var file = files[0];
        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();

        vm.SourceText = text;
        vm.DocumentTitle = file.Name;
        vm.StatusText = $"{file.Name} • {text.Length} chars";
    }

    private async Task OpenSettingsAsync()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var draft = vm.Settings.Clone();
        var settingsVm = new SettingsViewModel(ServiceLocator.Settings, draft);
        var window = new SettingsWindow { DataContext = settingsVm };

        var result = await window.ShowDialog<AppSettings?>(this);
        if (result is not null)
        {
            vm.ApplySettings(result);
            vm.StatusText = "Settings saved";
        }
    }
}
