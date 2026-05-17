using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using Markus.ViewModels;

namespace Markus.Services;

internal static class AppCommands
{
    public static ICommand Preferences { get; } = new RelayCommand(OpenPreferences);

    public static ICommand About { get; } = new RelayCommand(ShowAbout);

    public static ICommand Quit { get; } = new RelayCommand(QuitApp);

    private static void OpenPreferences()
    {
        if (
            Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow?.DataContext is MainWindowViewModel vm
        )
        {
            vm.OpenSettingsCommand.Execute(null);
        }
    }

    private static void ShowAbout()
    {
        // Placeholder. A proper About window will land alongside the
        // app-info plumbing.
    }

    private static void QuitApp()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
