namespace Markus.Models;

internal sealed class AppSettings
{
    public RendererKind Renderer { get; set; } = RendererKind.Native;

    public string Language { get; set; } = "en";

    public string Theme { get; set; } = "GitHubDark";

    public ViewMode DefaultViewMode { get; set; } = ViewMode.Preview;

    public bool ShowOutline { get; set; }

    public double FontSize { get; set; } = 16.0;

    public string MonoFont { get; set; } = "Iosevka";

    public string ThemeMode { get; set; } = "System";

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Renderer = Renderer,
            Language = Language,
            Theme = Theme,
            ThemeMode = ThemeMode,
            DefaultViewMode = DefaultViewMode,
            ShowOutline = ShowOutline,
            FontSize = FontSize,
            MonoFont = MonoFont,
        };
    }
}
