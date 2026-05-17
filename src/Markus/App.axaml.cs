using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Markus.ViewModels;
using Markus.Views;

namespace Markus;

internal sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var settings = Services.ServiceLocator.Settings.Load();
        Services.ThemeApplicator.Apply(settings.ThemeMode);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = vm };
            // Files passed on the command line cover Windows / Linux launchers
            // and macOS terminal launches.
            FileOpenRouter.OpenInitial(vm, desktop.Args);
            // macOS Finder double-clicks (both the initial launch document and
            // subsequent files dropped on a running Markus) arrive through the
            // NSApplicationDelegate's application:openFiles:, which Avalonia 12
            // no longer forwards to the C# side. We swap in our own IMP.
            Views.Platform.MacosAppleEventHandler.Register(path => FileOpenRouter.OpenSingle(vm, path));
        }

        base.OnFrameworkInitializationCompleted();
    }
}

internal static class FileOpenRouter
{
    public static void OpenInitial(MainWindowViewModel vm, IReadOnlyList<string>? args)
    {
        if (args is null)
        {
            return;
        }
        var path = FirstReadableFile(args);
        if (path is null)
        {
            return;
        }
        Dispatcher.UIThread.Post(() => _ = LoadFileAsync(vm, path));
    }

    public static void OpenSingle(MainWindowViewModel vm, string rawPath)
    {
        var path = NormalizePath(rawPath);
        if (!System.IO.File.Exists(path))
        {
            return;
        }
        Dispatcher.UIThread.Post(() => _ = LoadFileAsync(vm, path));
    }

    public static string? FirstReadableFile(IReadOnlyList<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            var normalized = NormalizePath(candidate);
            if (System.IO.File.Exists(normalized))
            {
                return normalized;
            }
        }
        return null;
    }

    public static string NormalizePath(string raw)
    {
        if (!raw.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return raw;
        }
        return uri.LocalPath;
    }

    private static async System.Threading.Tasks.Task LoadFileAsync(MainWindowViewModel vm, string path)
    {
        try
        {
            await vm.LoadFileAsync(path);
        }
        catch (System.IO.IOException)
        {
            vm.StatusText = $"{System.IO.Path.GetFileName(path)} • read failed";
        }
        catch (UnauthorizedAccessException)
        {
            vm.StatusText = $"{System.IO.Path.GetFileName(path)} • permission denied";
        }
    }
}
