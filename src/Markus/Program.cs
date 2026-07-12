using Avalonia;

namespace Markus;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called. Things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Services.StartupTrace.Mark("main-entry");
        // Install the process-wide crash safety net before anything else runs so
        // an early fault still writes a crash log and dumps unsaved work. The
        // dispatcher-thread hook is added later, once Avalonia is up.
        CrashHandler.InstallProcessHandlers();
        var app = BuildAvaloniaApp();
        Services.StartupTrace.Mark("avalonia-app-built");
        app.StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration. Don't remove. Also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UsePlatformDetect()
#if DEBUG
        .WithDeveloperTools()
#endif
        .LogToTrace();
    }
}
