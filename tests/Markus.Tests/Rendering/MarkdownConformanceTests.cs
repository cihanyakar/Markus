using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Markus.Rendering;
using Markus.Services;

namespace Markus.Tests.Rendering;

// Deep conformance against GFM (strikethrough, emphasis extras, table column
// alignment), Unicode emoji handling (UTS #51 grapheme clusters), and East
// Asian Width (UAX #11).
public sealed class MarkdownConformanceTests
{
    // --- GFM emphasis: the delimiter character decides the style ---

    [Fact]
    public void Strikethrough_RendersStrikethroughNotBold()
    {
        FindSpan(Render("~~struck~~"), s => HasDecoration(s, TextDecorationLocation.Strikethrough)).ShouldNotBeNull();
    }

    [Fact]
    public void Subscript_RendersBaselineSubscript()
    {
        FindSpan(Render("H~2~O"), s => s.BaselineAlignment == BaselineAlignment.Subscript).ShouldNotBeNull();
    }

    [Fact]
    public void Superscript_RendersBaselineSuperscript()
    {
        FindSpan(Render("x^2^"), s => s.BaselineAlignment == BaselineAlignment.Superscript).ShouldNotBeNull();
    }

    [Fact]
    public void Inserted_RendersUnderline()
    {
        FindSpan(Render("++ins++"), s => HasDecoration(s, TextDecorationLocation.Underline)).ShouldNotBeNull();
    }

    [Fact]
    public void Marked_RendersBackgroundHighlight()
    {
        FindSpan(Render("==mark=="), s => s.Background is not null).ShouldNotBeNull();
    }

    [Fact]
    public void BoldAndItalic_StillRender()
    {
        FindSpan(Render("**b**"), s => s.FontWeight == FontWeight.Bold).ShouldNotBeNull();
        FindSpan(Render("*i*"), s => s.FontStyle == FontStyle.Italic).ShouldNotBeNull();
    }

    // --- GFM table column alignment ---

    [Theory]
    [InlineData(0, HorizontalAlignment.Left)]
    [InlineData(1, HorizontalAlignment.Center)]
    [InlineData(2, HorizontalAlignment.Right)]
    public void Table_AppliesColumnAlignment(int column, HorizontalAlignment expected)
    {
        var grid = (Grid)Render("| L | C | R |\n|:--|:-:|--:|\n| a | b | c |");

        CellContent(grid, row: 1, column).HorizontalAlignment.ShouldBe(expected);
    }

    // --- UTS #51 emoji: grapheme clusters stay intact and isolated ---

    [Theory]
    [InlineData("🚀")] // single
    [InlineData("👨‍💻")] // ZWJ sequence
    [InlineData("👨‍👩‍👧‍👦")] // multi-person ZWJ
    [InlineData("🇹🇷")] // flag (regional indicators)
    [InlineData("👍🏽")] // skin-tone modifier
    [InlineData("1️⃣")] // keycap sequence
    public void Emoji_ClusterStaysIntactAndIsolated(string emoji)
    {
        // The whole cluster occupies a single run (not split mid-sequence), kept
        // apart from the following text so the trailing space stays body-font.
        TopLevelRunTexts(Render($"{emoji} tail")).ShouldContain(emoji);
    }

    [Fact]
    public void Emoji_RunUsesEmojiFont()
    {
        var emojiRun = TopLevelRuns(Render("🚀 x")).First(r => string.Equals(r.Text, "🚀", StringComparison.Ordinal));

        emojiRun.FontFamily.Name.ShouldContain("Emoji");
    }

    // --- UAX #11 East Asian Width ---

    [Theory]
    [InlineData("ab", 2)] // ASCII narrow
    [InlineData("中文", 4)] // CJK ideographs (wide)
    [InlineData("日本語", 6)] // Kanji
    [InlineData("한국어", 6)] // Hangul syllables (wide)
    [InlineData("ひらがな", 8)] // Hiragana (wide)
    [InlineData("ａｂ", 4)] // fullwidth latin (wide)
    [InlineData("ｱｲｳ", 3)] // halfwidth katakana (narrow)
    [InlineData("a中", 3)] // mixed
    [InlineData("🚀", 2)] // emoji (wide)
    [InlineData("𠀀", 2)] // CJK Extension B (surrogate pair, wide)
    public void DisplayWidth_FollowsEastAsianWidth(string text, int expected)
    {
        MarkdownTableFormatter.DisplayWidth(text).ShouldBe(expected);
    }

    private static Control Render(string markdown)
    {
        return MarkdownRenderer.Render(MarkdownPipeline.Parse(markdown)).First().Control;
    }

    private static StackPanel CellContent(Grid grid, int row, int column)
    {
        var cell = grid.Children.OfType<Border>().First(b => Grid.GetRow(b) == row && Grid.GetColumn(b) == column);
        return (StackPanel)cell.Child!;
    }

    private static Span? FindSpan(Control control, Func<Span, bool> predicate)
    {
        return control is TextBlock tb ? FindSpan(tb.Inlines, predicate) : null;
    }

    private static Span? FindSpan(InlineCollection? inlines, Func<Span, bool> predicate)
    {
        if (inlines is null)
        {
            return null;
        }
        foreach (var inline in inlines)
        {
            if (inline is Span span)
            {
                if (predicate(span))
                {
                    return span;
                }
                var nested = FindSpan(span.Inlines, predicate);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }
        return null;
    }

    private static bool HasDecoration(Span span, TextDecorationLocation location)
    {
        // Compare the decoration collection by reference instead of reading
        // TextDecoration.Location. The renderer assigns Avalonia's shared static
        // presets (TextDecorations.Strikethrough / .Underline), and reading a
        // StyledProperty on those static objects calls Dispatcher.VerifyAccess,
        // which throws when the runner schedules this test on a thread other than
        // the one that first bound Dispatcher.UIThread. Reference identity needs
        // no UI thread, so it stays deterministic regardless of scheduling.
        var decorations = span.TextDecorations;
        if (decorations is null)
        {
            return false;
        }
        var expected = location switch
        {
            TextDecorationLocation.Strikethrough => TextDecorations.Strikethrough,
            TextDecorationLocation.Underline => TextDecorations.Underline,
            TextDecorationLocation.Overline => TextDecorations.Overline,
            TextDecorationLocation.Baseline => TextDecorations.Baseline,
            _ => null,
        };
        return expected is not null && ReferenceEquals(decorations, expected);
    }

    private static List<Run> TopLevelRuns(Control control)
    {
        return control is TextBlock tb ? tb.Inlines!.OfType<Run>().ToList() : new List<Run>();
    }

    private static List<string> TopLevelRunTexts(Control control)
    {
        return TopLevelRuns(control).Where(r => r.Text is not null).Select(r => r.Text!).ToList();
    }
}
