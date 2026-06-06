using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Markus.Rendering;
using Markus.Services;

namespace Markus.Tests.Rendering;

// Structural conformance tests: feed Markdown through the real parse + render
// pipeline and assert the produced Avalonia control tree, including regression
// coverage for CommonMark behaviors (soft vs hard breaks, HTML entities).
public sealed class MarkdownRenderStructureTests
{
    [Fact]
    public void Heading_ProducesTextBlock_WithText()
    {
        var blocks = Render("# Hello world");

        var text = InlineText(blocks[0].Control);
        text.ShouldBe("Hello world");
    }

    [Fact]
    public void SoftBreak_JoinsLinesWithSpace()
    {
        // CommonMark: a single newline is a soft break, rendered as a space so
        // the source lines flow together.
        var blocks = Render("alpha\nbeta");

        InlineText(blocks[0].Control).ShouldBe("alpha beta");
    }

    [Fact]
    public void HardBreak_DoesNotCollapseToSpace()
    {
        // A backslash line break is hard: the two halves stay on separate lines,
        // so the joined text is NOT "alpha beta".
        var blocks = Render("alpha\\\nbeta");

        InlineText(blocks[0].Control).ShouldNotBe("alpha beta");
        InlineText(blocks[0].Control).ShouldContain("alpha");
        InlineText(blocks[0].Control).ShouldContain("beta");
    }

    [Fact]
    public void HtmlEntities_DecodeToCharacters()
    {
        var blocks = Render("x &copy; &amp; &#42; y");

        InlineText(blocks[0].Control).ShouldBe("x © & * y");
    }

    [Fact]
    public void Emphasis_RendersBoldAndItalicText()
    {
        var blocks = Render("plain **bold** and *italic*");

        InlineText(blocks[0].Control).ShouldBe("plain bold and italic");
    }

    [Fact]
    public void Link_WithFormattedText_RendersTheTextNotTheUrl()
    {
        // A link whose text is bold / code / multi-part must render that text,
        // not collapse to the first literal or fall back to the raw URL.
        var blocks = Render("[**bold** and `code`](https://example.com)");

        var text = InlineText(blocks[0].Control);
        text.ShouldBe("bold and code");
        text.ShouldNotContain("example.com");
    }

    [Fact]
    public void FencedCode_ProducesBorderedCard()
    {
        var blocks = Render("```\ncode line\n```");

        blocks[0].Control.ShouldBeOfType<Border>();
    }

    [Fact]
    public void HashInsideFence_IsNotAHeading()
    {
        var blocks = Render("```\n# not a heading\n```");

        // One block: the code card. A stray heading would add a second block.
        blocks.Count.ShouldBe(1);
        blocks[0].Control.ShouldBeOfType<Border>();
    }

    [Fact]
    public void UnorderedList_ProducesStackPanel()
    {
        var blocks = Render("- one\n- two");

        blocks[0].Control.ShouldBeOfType<StackPanel>();
    }

    [Fact]
    public void Table_ProducesGrid()
    {
        var blocks = Render("| a | b |\n|---|---|\n| 1 | 2 |");

        blocks[0].Control.ShouldBeOfType<Grid>();
    }

    [Fact]
    public void ThematicBreak_ProducesBorder()
    {
        var blocks = Render("---");

        blocks[0].Control.ShouldBeOfType<Border>();
    }

    private static List<RenderedBlock> Render(string markdown)
    {
        return MarkdownRenderer.Render(MarkdownPipeline.Parse(markdown)).ToList();
    }

    private static string InlineText(Control control)
    {
        return control is TextBlock tb ? Flatten(tb.Inlines) : string.Empty;
    }

    private static string Flatten(InlineCollection? inlines)
    {
        if (inlines is null)
        {
            return string.Empty;
        }
        var sb = new System.Text.StringBuilder();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    sb.Append(run.Text);
                    break;
                case Span span:
                    sb.Append(Flatten(span.Inlines));
                    break;
                case LineBreak:
                    sb.Append('\n');
                    break;
            }
        }
        return sb.ToString();
    }
}
