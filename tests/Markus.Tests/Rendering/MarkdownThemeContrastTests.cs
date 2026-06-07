using Avalonia.Media;
using Markus.Rendering;

namespace Markus.Tests.Rendering;

// WCAG 2.x contrast conformance: every built-in preview theme must keep body
// text, links, and muted text legible (AA, 4.5:1) against its background.
public sealed class MarkdownThemeContrastTests
{
    private const double AaNormal = 4.5;

    public static IEnumerable<object[]> ThemeKeys => MarkdownThemes.All.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(ThemeKeys))]
    public void BodyText_MeetsAa(string key)
    {
        var theme = MarkdownThemes.Resolve(key);

        Contrast(theme.Foreground, theme.Background).ShouldBeGreaterThanOrEqualTo(AaNormal);
    }

    [Theory]
    [MemberData(nameof(ThemeKeys))]
    public void Links_MeetAa(string key)
    {
        var theme = MarkdownThemes.Resolve(key);

        Contrast(theme.Accent, theme.Background).ShouldBeGreaterThanOrEqualTo(AaNormal);
    }

    [Theory]
    [MemberData(nameof(ThemeKeys))]
    public void MutedText_MeetsAa(string key)
    {
        var theme = MarkdownThemes.Resolve(key);

        Contrast(theme.Muted, theme.Background).ShouldBeGreaterThanOrEqualTo(AaNormal);
    }

    private static double Contrast(Color a, Color b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        var hi = Math.Max(la, lb);
        var lo = Math.Min(la, lb);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double RelativeLuminance(Color c)
    {
        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }

    private static double Channel(byte value)
    {
        var c = value / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
