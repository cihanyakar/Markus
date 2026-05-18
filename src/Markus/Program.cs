using Avalonia;
using Fonts.Avalonia.CascadiaCode;
using Fonts.Avalonia.JetBrainsMono;
using Fonts.Avalonia.Manrope;

namespace Markus;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called. Things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration. Don't remove. Also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .WithManropeFont()
            .WithJetBrainsMonoFont()
            .WithCascadiaCodeFont()
            .LogToTrace();
    }
}
