using Avalonia.Media;

namespace Markus.Rendering;

internal static class MarkdownThemes
{
    public static readonly MarkdownTheme GitHubLight = new MarkdownTheme
    {
        Key = "GitHubLight",
        DisplayName = "GitHub Light",
        IsDark = false,
        Background = Color.FromRgb(0xFF, 0xFF, 0xFF),
        Foreground = Color.FromRgb(0x1F, 0x23, 0x28),
        Accent = Color.FromRgb(0x09, 0x69, 0xDA),
        CodeBackground = Color.FromArgb(0x14, 0x00, 0x00, 0x00),
        CodeForeground = Color.FromRgb(0x1F, 0x23, 0x28),
        CodeBorder = Color.FromArgb(0x33, 0x00, 0x00, 0x00),
        QuoteAccent = Color.FromArgb(0x99, 0x57, 0x60, 0x6A),
        Muted = Color.FromRgb(0x57, 0x60, 0x6A),
    };

    public static readonly MarkdownTheme GitHubDark = new MarkdownTheme
    {
        Key = "GitHubDark",
        DisplayName = "GitHub Dark",
        IsDark = true,
        Background = Color.FromRgb(0x0D, 0x11, 0x17),
        Foreground = Color.FromRgb(0xE6, 0xED, 0xF3),
        Accent = Color.FromRgb(0x58, 0xA6, 0xFF),
        CodeBackground = Color.FromArgb(0x55, 0x16, 0x1B, 0x22),
        CodeForeground = Color.FromRgb(0xE6, 0xED, 0xF3),
        CodeBorder = Color.FromArgb(0x55, 0x30, 0x36, 0x3D),
        QuoteAccent = Color.FromArgb(0xAA, 0x8B, 0x94, 0x9E),
        Muted = Color.FromRgb(0x8B, 0x94, 0x9E),
    };

    public static readonly MarkdownTheme SolarizedLight = new MarkdownTheme
    {
        Key = "SolarizedLight",
        DisplayName = "Solarized Light",
        IsDark = false,
        Background = Color.FromRgb(0xFD, 0xF6, 0xE3),
        Foreground = Color.FromRgb(0x58, 0x6E, 0x75),
        // Darkened from Solarized's #268BD2 / #657B83 to clear WCAG AA (4.5:1)
        // for links and muted text on the light base3 background.
        Accent = Color.FromRgb(0x1F, 0x72, 0xAC),
        CodeBackground = Color.FromArgb(0x55, 0xEE, 0xE8, 0xD5),
        CodeForeground = Color.FromRgb(0x58, 0x6E, 0x75),
        CodeBorder = Color.FromArgb(0x55, 0x93, 0xA1, 0xA1),
        QuoteAccent = Color.FromArgb(0xAA, 0xB5, 0x89, 0x00),
        Muted = Color.FromRgb(0x5D, 0x71, 0x79),
    };

    public static readonly MarkdownTheme SolarizedDark = new MarkdownTheme
    {
        Key = "SolarizedDark",
        DisplayName = "Solarized Dark",
        IsDark = true,
        Background = Color.FromRgb(0x00, 0x2B, 0x36),
        Foreground = Color.FromRgb(0x93, 0xA1, 0xA1),
        Accent = Color.FromRgb(0xB5, 0x89, 0x00),
        CodeBackground = Color.FromArgb(0x55, 0x07, 0x36, 0x42),
        CodeForeground = Color.FromRgb(0x93, 0xA1, 0xA1),
        CodeBorder = Color.FromArgb(0x55, 0x58, 0x6E, 0x75),
        QuoteAccent = Color.FromArgb(0xAA, 0x26, 0x8B, 0xD2),
        Muted = Color.FromRgb(0x83, 0x94, 0x96),
    };

    public static readonly MarkdownTheme Nord = new MarkdownTheme
    {
        Key = "Nord",
        DisplayName = "Nord",
        IsDark = true,
        Background = Color.FromRgb(0x2E, 0x34, 0x40),
        Foreground = Color.FromRgb(0xEC, 0xEF, 0xF4),
        Accent = Color.FromRgb(0x88, 0xC0, 0xD0),
        CodeBackground = Color.FromArgb(0x55, 0x3B, 0x42, 0x52),
        CodeForeground = Color.FromRgb(0xEC, 0xEF, 0xF4),
        CodeBorder = Color.FromArgb(0x55, 0x4C, 0x56, 0x6A),
        QuoteAccent = Color.FromArgb(0xAA, 0x81, 0xA1, 0xC1),
        // Lightened from Nord's #818C9F to clear WCAG AA on the dark polar bg.
        Muted = Color.FromRgb(0x95, 0x9E, 0xAE),
    };

    public static readonly MarkdownTheme TokyoNight = new MarkdownTheme
    {
        Key = "TokyoNight",
        DisplayName = "Tokyo Night",
        IsDark = true,
        Background = Color.FromRgb(0x1A, 0x1B, 0x26),
        Foreground = Color.FromRgb(0xC0, 0xCA, 0xF5),
        Accent = Color.FromRgb(0x7A, 0xA2, 0xF7),
        CodeBackground = Color.FromArgb(0x55, 0x1A, 0x1B, 0x26),
        CodeForeground = Color.FromRgb(0xC0, 0xCA, 0xF5),
        CodeBorder = Color.FromArgb(0x55, 0x41, 0x48, 0x68),
        QuoteAccent = Color.FromArgb(0xAA, 0xBB, 0x9A, 0xF7),
        Muted = Color.FromRgb(0x9A, 0xA5, 0xCE),
    };

    public static readonly IReadOnlyDictionary<string, MarkdownTheme> All = new Dictionary<string, MarkdownTheme>(
        StringComparer.Ordinal
    )
    {
        [GitHubLight.Key] = GitHubLight,
        [GitHubDark.Key] = GitHubDark,
        [SolarizedLight.Key] = SolarizedLight,
        [SolarizedDark.Key] = SolarizedDark,
        [Nord.Key] = Nord,
        [TokyoNight.Key] = TokyoNight,
    };

    public static MarkdownTheme Resolve(string key)
    {
        return All.TryGetValue(key, out var theme) ? theme : GitHubDark;
    }
}
