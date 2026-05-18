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
    // Set synchronously the moment we claim a document (from argv or from the
    // first AppleEvent). Subsequent runtime opens spawn a fresh process so
    // each "Open With" lands in its own window. Without this flag, the spawn's
    // own openURLs echo would itself trigger another spawn — infinite loop.
    private static bool _hasInitialDoc;

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
        _hasInitialDoc = true;
        Dispatcher.UIThread.Post(() => _ = LoadFileAsync(vm, path));
    }

    public static void OpenSingle(MainWindowViewModel vm, string rawPath)
    {
        var path = NormalizePath(rawPath);
        if (!System.IO.File.Exists(path))
        {
            return;
        }
        // First open event this process sees becomes the initial document.
        // Covers Finder launches where argv is empty and the file arrives via
        // AppleEvent only — including the spawned child below.
        if (!_hasInitialDoc)
        {
            _hasInitialDoc = true;
            Dispatcher.UIThread.Post(() => _ = LoadFileAsync(vm, path));
            return;
        }
        // Already have a document: every subsequent open lands in a fresh
        // process so users get one window per document.
        if (TrySpawnNewInstance(path))
        {
            return;
        }
        // Fallback (non-macOS, missing bundle): load in current as a graceful
        // degradation.
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

    private static bool TrySpawnNewInstance(string path)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }
        var bundlePath = ResolveBundlePath();
        if (bundlePath is null)
        {
            return false;
        }
        try
        {
            // `open -n` forces a new process even when the .app is already
            // running. Passing the file as a positional argument (not via
            // --args) makes LaunchServices deliver it through the standard
            // openURLs AppleEvent rather than argv. That keeps argv empty in
            // the spawned child so its OpenInitial does nothing, and the
            // _hasInitialDoc gate consumes the AppleEvent as the child's
            // first document instead of looping back into another spawn.
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/bin/open",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-n");
            psi.ArgumentList.Add("-a");
            psi.ArgumentList.Add(bundlePath);
            psi.ArgumentList.Add(path);
            System.Diagnostics.Process.Start(psi);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        catch (System.IO.FileNotFoundException)
        {
            return false;
        }
    }

    private static string? ResolveBundlePath()
    {
        // Inside a .app, AppContext.BaseDirectory is .../Markus.app/Contents/MacOS/.
        // Walk up two levels to find the bundle root.
        var baseDir = AppContext.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar);
        var contents = System.IO.Path.GetDirectoryName(baseDir);
        if (contents is null)
        {
            return null;
        }
        var bundle = System.IO.Path.GetDirectoryName(contents);
        if (bundle is null || !bundle.EndsWith(".app", StringComparison.Ordinal))
        {
            return null;
        }
        return bundle;
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
