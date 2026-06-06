using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Markus.ViewModels;
using Markus.Views;

namespace Markus;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Reliability",
    "S2930:IDisposables should be disposed",
    Justification = "ShutdownCts lives for the entire process lifetime."
)]
internal sealed partial class App : Application
{
    private static readonly CancellationTokenSource ShutdownCts = new();

    public static CancellationToken ShutdownToken => ShutdownCts.Token;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        PosixSignalRegistration.Create(PosixSignal.SIGINT, _ => ShutdownCts.Cancel());
        PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => ShutdownCts.Cancel());

        var settings = Services.ServiceLocator.Settings.Load();
        Services.ThemeApplicator.Apply(settings.ThemeMode);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += (_, _) => ShutdownCts.Cancel();

            var vm = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = vm };
            var isSpawnedChild = FileOpenRouter.IsSpawnMarker(desktop.Args);
            var updateVm = new ViewModels.UpdateViewModel(
                new Services.Updates.UpdateChecker(new Services.Updates.GitHubReleaseFeed()),
                new Services.Updates.AssemblyVersionProvider(),
                new Services.Updates.UpdateDownloader(),
                new Services.Updates.UpdateLauncher(),
                Services.ServiceLocator.Settings,
                Services.Updates.RuntimeRid.Current
            );
            vm.Update = updateVm;
            FileOpenRouter.OpenInitial(vm, desktop.Args);
            FileOpenRouter.MaybeRestoreSession(vm, settings, isSpawnedChild);
            Views.Platform.MacosAppleEventHandler.Register(path => FileOpenRouter.OpenSingle(vm, path));
            FileOpenRouter.BeginAwaitInitialDocument(vm);
            if (
                Services.Updates.UpdatePolicy.ShouldAutoCheck(
                    settings.CheckForUpdatesOnLaunch,
                    isSpawnedChild,
                    settings.LastUpdateCheckUtc,
                    DateTimeOffset.UtcNow,
                    TimeSpan.FromHours(20)
                )
            )
            {
                Dispatcher.UIThread.Post(() => _ = updateVm.CheckOnLaunchAsync(App.ShutdownToken));
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}

internal enum FileOpenDecision
{
    FocusExisting,
    LoadInCurrent,
    SpawnNewInstance,
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
        Dispatcher.UIThread.Post(() => _ = LoadFileAsync(vm, path, App.ShutdownToken));
    }

    // A Finder double-click (or an "Open With" spawn) delivers its document
    // through an AppleEvent that lands just after the window is shown, not via
    // argv. Without this, the window paints the welcome screen first and the
    // document flashes in a beat later. Hide the welcome for a short grace
    // period so the incoming document takes its place with no empty render. If
    // no document arrives (a plain Dock launch), the welcome is revealed.
    public static void BeginAwaitInitialDocument(MainWindowViewModel vm)
    {
        if (!OperatingSystem.IsMacOS() || _hasInitialDoc)
        {
            return;
        }
        vm.IsAwaitingInitialDocument = true;
        DispatcherTimer.RunOnce(() => vm.IsAwaitingInitialDocument = false, TimeSpan.FromMilliseconds(350));
    }

    public static bool ConsumeInitialDocSlot()
    {
        if (_hasInitialDoc)
        {
            return false;
        }
        _hasInitialDoc = true;
        return true;
    }

    public static void MaybeRestoreSession(
        MainWindowViewModel vm,
        Markus.Models.AppSettings settings,
        bool isSpawnedChild
    )
    {
        if (_hasInitialDoc)
        {
            return;
        }
        var path = settings.LastOpenedFile;
        var lastFileExists = !string.IsNullOrEmpty(path) && System.IO.File.Exists(path);
        if (!ShouldRestoreSession(isSpawnedChild, settings.RestoreSessionOnLaunch, lastFileExists))
        {
            return;
        }
        _hasInitialDoc = true;
        Dispatcher.UIThread.Post(() => _ = LoadFileAsync(vm, path!, App.ShutdownToken));
    }

    // A spawned child (launched by TrySpawnNewInstance) must NOT restore the
    // last session: it exists solely to display the document delivered by the
    // open event. Restoring would consume the initial-doc slot before that
    // event arrives, forcing the document down the spawn path again and
    // cascading new windows on every open while restore-on-launch is enabled.
    public static bool ShouldRestoreSession(bool isSpawnedChild, bool restoreEnabled, bool lastFileExists)
    {
        return !isSpawnedChild && restoreEnabled && lastFileExists;
    }

    // The spawn flag is delivered as a plain argv token (after `open --args`)
    // rather than an environment variable, because LaunchServices does not
    // propagate the parent's environment to the launched bundle.
    public static bool IsSpawnMarker(IReadOnlyList<string>? args)
    {
        return args is not null && args.Contains("--spawned", StringComparer.Ordinal);
    }

    public static void OpenSingle(MainWindowViewModel vm, string rawPath)
    {
        var path = NormalizePath(rawPath);
        if (!System.IO.File.Exists(path))
        {
            return;
        }
        switch (DecideOpen(_hasInitialDoc, vm.CurrentFilePath, path))
        {
            case FileOpenDecision.FocusExisting:
                // The requested file is already on screen here: surface this
                // window instead of opening a duplicate copy.
                Dispatcher.UIThread.Post(ActivateMainWindow);
                return;
            case FileOpenDecision.LoadInCurrent:
                _hasInitialDoc = true;
                Dispatcher.UIThread.Post(() => _ = LoadFileAsync(vm, path, App.ShutdownToken));
                return;
            default:
                if (TrySpawnNewInstance(path))
                {
                    return;
                }
                // Fallback (non-macOS, missing bundle): load in current as a
                // graceful degradation.
                Dispatcher.UIThread.Post(() => _ = LoadFileAsync(vm, path, App.ShutdownToken));
                return;
        }
    }

    // Routing decision for an incoming open request, factored out so it can be
    // unit-tested without the process/dispatcher side effects.
    public static FileOpenDecision DecideOpen(bool hasInitialDoc, string? currentPath, string requestedPath)
    {
        if (SamePath(currentPath, requestedPath))
        {
            return FileOpenDecision.FocusExisting;
        }
        if (!hasInitialDoc)
        {
            return FileOpenDecision.LoadInCurrent;
        }
        return FileOpenDecision.SpawnNewInstance;
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

    private static bool SamePath(string? a, string b)
    {
        if (string.IsNullOrEmpty(a))
        {
            return false;
        }
        return string.Equals(Canonical(a), Canonical(b), StringComparison.OrdinalIgnoreCase);
    }

    private static string Canonical(string path)
    {
        try
        {
            return System.IO.Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }

    private static void ActivateMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Activate();
        }
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
            // Everything after --args reaches the child as argv. The file stays
            // before it so LaunchServices still delivers it via the openURLs
            // AppleEvent, while --spawned marks the child so it skips session
            // restore (see ShouldRestoreSession).
            psi.ArgumentList.Add("--args");
            psi.ArgumentList.Add("--spawned");
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

    private static async System.Threading.Tasks.Task LoadFileAsync(
        MainWindowViewModel vm,
        string path,
        CancellationToken ct
    )
    {
        // A document is on its way in; drop the welcome-suppression grace flag
        // so the loaded content (not the welcome screen) is what appears.
        vm.IsAwaitingInitialDocument = false;
        try
        {
            await vm.LoadFileAsync(path, ct);
        }
        catch (OperationCanceledException)
        {
            // Shutdown or signal interrupted the read.
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
