namespace Markus.Models;

internal sealed class AppSettings
{
    public RendererKind Renderer { get; set; } = RendererKind.Native;

    public string Language { get; set; } = "en";

    public string Theme { get; set; } = "GitHubDark";

    public string CodeTheme { get; set; } = "Auto";

    public ViewMode DefaultViewMode { get; set; } = ViewMode.Preview;

    public bool ShowOutline { get; set; }

    public double FontSize { get; set; } = 16.0;

    public string MonoFont { get; set; } = "JetBrains Mono";

    public string ThemeMode { get; set; } = "System";

    public bool IsSourceSoftWrap { get; set; }

    public bool IsPreviewSoftWrap { get; set; } = true;

    public List<string> RecentFiles { get; set; } = new List<string>();

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Renderer = Renderer,
            Language = Language,
            Theme = Theme,
            CodeTheme = CodeTheme,
            ThemeMode = ThemeMode,
            DefaultViewMode = DefaultViewMode,
            ShowOutline = ShowOutline,
            FontSize = FontSize,
            MonoFont = MonoFont,
            IsSourceSoftWrap = IsSourceSoftWrap,
            IsPreviewSoftWrap = IsPreviewSoftWrap,
            RecentFiles = new List<string>(RecentFiles),
        };
    }
}
