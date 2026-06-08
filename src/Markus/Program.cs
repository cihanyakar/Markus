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
