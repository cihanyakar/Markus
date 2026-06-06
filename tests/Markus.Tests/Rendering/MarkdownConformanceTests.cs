using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Markus.Rendering;
using Markus.Services;

namespace Markus.Tests.Rendering;

// Conformance against GFM (strikethrough), Unicode emoji handling (UTS #51),
// and East Asian Width (UAX #11).
public sealed class MarkdownConformanceTests
{
    [Fact]
    public void Strikethrough_RendersStrikethroughNotBold()
    {
        var block = Render("~~struck~~");

        HasStrikethrough(block).ShouldBeTrue();
    }

    [Fact]
    public void Emoji_IsIsolatedFromFollowingSpace()
    {
        // The emoji must occupy its own run so the trailing space is shaped in
        // the body font (otherwise it renders with a wide gap).
        var block = Render("🚀 launch");

        TopLevelRunTexts(block).ShouldContain("🚀");
    }

    [Theory]
    [InlineData("ab", 2)] // plain ASCII
    [InlineData("中文", 4)] // CJK ideographs are double width
    [InlineData("a中", 3)] // mixed
    [InlineData("🚀", 2)] // emoji is double width
    [InlineData("ｱ", 1)] // halfwidth katakana is single width
    public void DisplayWidth_FollowsEastAsianWidth(string text, int expected)
    {
        MarkdownTableFormatter.DisplayWidth(text).ShouldBe(expected);
    }

    private static Control Render(string markdown)
    {
        return MarkdownRenderer.Render(MarkdownPipeline.Parse(markdown)).First().Control;
    }

    private static bool HasStrikethrough(Control control)
    {
        return control is TextBlock tb && InlinesHaveStrikethrough(tb.Inlines);
    }

    private static bool InlinesHaveStrikethrough(InlineCollection? inlines)
    {
        if (inlines is null)
        {
            return false;
        }
        foreach (var inline in inlines)
        {
            if (inline is Span span)
            {
                var decorated =
                    span.TextDecorations?.Any(d => d.Location == TextDecorationLocation.Strikethrough) == true;
                if (decorated || InlinesHaveStrikethrough(span.Inlines))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static List<string> TopLevelRunTexts(Control control)
    {
        var texts = new List<string>();
        if (control is TextBlock tb)
        {
            foreach (var inline in tb.Inlines!)
            {
                if (inline is Run run && run.Text is { } t)
                {
                    texts.Add(t);
                }
            }
        }
        return texts;
    }
}
