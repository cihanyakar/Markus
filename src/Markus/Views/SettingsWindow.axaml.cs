using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Markus.ViewModels;

namespace Markus.Views;

internal sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Icon = Markus.Services.IconLoader.LoadWindowIcon();
    }

    private async void RecordShortcut_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not ShortcutBindingViewModel binding)
        {
            return;
        }
        var dialog = new ShortcutRecorderDialog();
        var gesture = await dialog.ShowDialog<KeyGesture?>(this);
        if (gesture is not null)
        {
            binding.Commit(gesture);
        }
    }

    private static void ResetShortcut_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ShortcutBindingViewModel binding)
        {
            binding.Reset();
        }
    }

    private void ResetAllShortcuts_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }
        Markus.Services.ServiceLocator.Keys.ResetAll();
        foreach (var row in vm.Shortcuts)
        {
            row.Reset();
        }
    }
}
