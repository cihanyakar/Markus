using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using Markus.ViewModels;
using Markus.Views;

namespace Markus.Services;

internal static class AppCommands
{
    public static ICommand Preferences { get; } = new RelayCommand(OpenPreferences);

    public static ICommand About { get; } = new RelayCommand(ShowAbout);

    public static ICommand CheckForUpdates { get; } = new RelayCommand(RunCheckForUpdates);

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
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }
        var owner = desktop.MainWindow;
        var window = new AboutWindow();
        if (owner is null)
        {
            window.Show();
            return;
        }
        _ = window.ShowDialog(owner);
    }

    private static void RunCheckForUpdates()
    {
        if (
            Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow?.DataContext is MainWindowViewModel vm
            && vm.Update is not null
        )
        {
            vm.Update.CheckForUpdatesCommand.Execute(null);
        }
    }

    private static void QuitApp()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
