namespace Markus.Models;

internal sealed class AppSettings
{
    public RendererKind Renderer { get; set; } = RendererKind.Native;

    public string Language { get; set; } = "en";

    public string Theme { get; set; } = "GitHubDark";

    public string CodeTheme { get; set; } = "Auto";

    public ViewMode DefaultViewMode { get; set; } = ViewMode.Preview;

    public bool ShowOutline { get; set; }

    public OutlinePlacement OutlinePlacement { get; set; } = OutlinePlacement.Right;

    public double FontSize { get; set; } = 16.0;

    public string MonoFont { get; set; } = "JetBrains Mono";

    public string ThemeMode { get; set; } = "System";

    public bool IsSourceSoftWrap { get; set; }

    public bool IsPreviewSoftWrap { get; set; } = true;

    public double MermaidScale { get; set; } = 1.0;

    public List<string> RecentFiles { get; set; } = new List<string>();

    public string? LastOpenedFile { get; set; }

    public int LastScrollLine { get; set; }

    public bool RestoreSessionOnLaunch { get; set; }

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
            OutlinePlacement = OutlinePlacement,
            FontSize = FontSize,
            MonoFont = MonoFont,
            IsSourceSoftWrap = IsSourceSoftWrap,
            IsPreviewSoftWrap = IsPreviewSoftWrap,
            MermaidScale = MermaidScale,
            RecentFiles = new List<string>(RecentFiles),
            LastOpenedFile = LastOpenedFile,
            LastScrollLine = LastScrollLine,
            RestoreSessionOnLaunch = RestoreSessionOnLaunch,
        };
    }
}
