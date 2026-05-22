using Markdig.Extensions.Emoji;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markus.Services;

namespace Markus.Tests.Services;

public sealed class MarkdownPipelineTests
{
    [Fact]
    public void Parse_NullInput_ReturnsEmptyDocument()
    {
        var doc = MarkdownPipeline.Parse(null!);

        doc.ShouldNotBeNull();
        doc.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyDocument()
    {
        var doc = MarkdownPipeline.Parse(string.Empty);

        doc.ShouldNotBeNull();
        doc.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_Heading_ProducesHeadingBlockWithLevelAndText()
    {
        var doc = MarkdownPipeline.Parse("## Section title");

        var heading = doc.OfType<HeadingBlock>().ShouldHaveSingleItem();
        heading.Level.ShouldBe(2);
        var text = string.Concat(heading.Inline!.Descendants<LiteralInline>().Select(l => l.Content.ToString()));
        text.ShouldBe("Section title");
    }

    [Fact]
    public void Parse_FencedCodeBlock_ProducesFencedCodeBlock()
    {
        var doc = MarkdownPipeline.Parse("```cs\nvar x = 1;\n```");

        doc.OfType<FencedCodeBlock>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Parse_TaskList_ProducesTaskListInlines()
    {
        var doc = MarkdownPipeline.Parse("- [x] done\n- [ ] todo\n");

        var tasks = doc.Descendants<TaskList>().ToList();
        tasks.Count.ShouldBe(2);
        tasks[0].Checked.ShouldBeTrue();
        tasks[1].Checked.ShouldBeFalse();
    }

    [Fact]
    public void Parse_YamlFrontMatter_AppearsAsFirstTopLevelBlock()
    {
        var doc = MarkdownPipeline.Parse("---\ntitle: Hello\n---\n# Body");

        doc.Count.ShouldBeGreaterThan(0);
        doc[0].ShouldBeOfType<YamlFrontMatterBlock>();
        doc.OfType<HeadingBlock>().ShouldHaveSingleItem().Level.ShouldBe(1);
    }

    [Fact]
    public void Parse_BareUrl_ProducesAutoLinkInline()
    {
        var doc = MarkdownPipeline.Parse("Check https://example.com here");

        var link = doc.Descendants<LinkInline>().ShouldHaveSingleItem();
        link.IsAutoLink.ShouldBeTrue();
        link.Url.ShouldBe("https://example.com");
    }

    [Fact]
    public void Parse_InlineMath_ProducesMathInlineWithContent()
    {
        var doc = MarkdownPipeline.Parse("$x = 1$");

        var math = doc.Descendants<MathInline>().ShouldHaveSingleItem();
        math.Content.ToString().ShouldBe("x = 1");
    }

    [Fact]
    public void Parse_BlockMath_ProducesMathBlockWithContent()
    {
        var doc = MarkdownPipeline.Parse("$$\nx = 1\n$$");

        var block = doc.OfType<MathBlock>().ShouldHaveSingleItem();
        block.Lines.ToString().ShouldContain("x = 1");
    }

    [Fact]
    public void Parse_Emoji_ReplacesShortcodeWithEmojiInline()
    {
        var doc = MarkdownPipeline.Parse(":smile:");

        var emoji = doc.Descendants<EmojiInline>().ShouldHaveSingleItem();
        emoji.Content.ToString().ShouldNotBe(":smile:");
        emoji.Content.ToString().ShouldNotBeNullOrEmpty();
        doc.Descendants<LiteralInline>()
            .Any(l => string.Equals(l.Content.ToString(), ":smile:", System.StringComparison.Ordinal))
            .ShouldBeFalse();
    }

    [Fact]
    public void Parse_PipeTable_ProducesTableBlock()
    {
        var doc = MarkdownPipeline.Parse("| a | b |\n| - | - |\n| 1 | 2 |\n");

        doc.OfType<Table>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmptyDocument()
    {
        var doc = MarkdownPipeline.Parse("   \n  \n   ");

        doc.ShouldNotBeNull();
        doc.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_MermaidFencedCodeBlock_ProducesFencedCodeBlockWithMermaidInfo()
    {
        var doc = MarkdownPipeline.Parse("```mermaid\ngraph TD;\n  A-->B;\n```");

        var block = doc.OfType<FencedCodeBlock>().ShouldHaveSingleItem();
        block.Info.ShouldBe("mermaid");
        block.Lines.ToString().ShouldContain("A-->B");
    }

    [Fact]
    public void Parse_FencedCodeBlockWithoutLanguage_ProducesFencedCodeBlockWithNullInfo()
    {
        var doc = MarkdownPipeline.Parse("```\nsome code\n```");

        var block = doc.OfType<FencedCodeBlock>().ShouldHaveSingleItem();
        block.Info.ShouldBeNullOrEmpty();
    }

    [Fact]
    public void Parse_MultipleFencedCodeBlocks_ProducesAllBlocks()
    {
        var markdown = "```cs\nvar x = 1;\n```\n\n```mermaid\ngraph TD;\n```\n\n```json\n{}\n```";
        var doc = MarkdownPipeline.Parse(markdown);

        var blocks = doc.OfType<FencedCodeBlock>().ToList();
        blocks.Count.ShouldBe(3);
        blocks[0].Info.ShouldBe("cs");
        blocks[1].Info.ShouldBe("mermaid");
        blocks[2].Info.ShouldBe("json");
    }

    [Fact]
    public void Parse_BlockMathMultiLineComplex_PreservesAllContent()
    {
        var doc = MarkdownPipeline.Parse("$$\nx^2 + y^2 = z^2\n\\sum_{i=0}^{n} i\n$$");

        var block = doc.OfType<MathBlock>().ShouldHaveSingleItem();
        var content = block.Lines.ToString();
        content.ShouldContain("x^2 + y^2 = z^2");
        content.ShouldContain("\\sum_{i=0}^{n} i");
    }

    [Fact]
    public void Parse_InlineMathComplexExpression_PreservesContent()
    {
        var doc = MarkdownPipeline.Parse("The formula $\\frac{a}{b} + \\sqrt{c}$ is important.");

        var math = doc.Descendants<MathInline>().ShouldHaveSingleItem();
        math.Content.ToString().ShouldBe("\\frac{a}{b} + \\sqrt{c}");
    }

    [Fact]
    public void Parse_MultipleInlineMath_ProducesCorrectContentForEach()
    {
        var doc = MarkdownPipeline.Parse("Given $a = 1$ and $b = 2$ then $a + b = 3$.");

        var maths = doc.Descendants<MathInline>().ToList();
        maths.Count.ShouldBe(3);
        maths[0].Content.ToString().ShouldBe("a = 1");
        maths[1].Content.ToString().ShouldBe("b = 2");
        maths[2].Content.ToString().ShouldBe("a + b = 3");
    }

    [Fact]
    public void Parse_MultipleHeadingLevels_ProducesCorrectLevels()
    {
        var doc = MarkdownPipeline.Parse("# H1\n## H2\n### H3\n#### H4");

        var headings = doc.OfType<HeadingBlock>().ToList();
        headings.Count.ShouldBe(4);
        headings[0].Level.ShouldBe(1);
        headings[1].Level.ShouldBe(2);
        headings[2].Level.ShouldBe(3);
        headings[3].Level.ShouldBe(4);
    }

    [Fact]
    public void Parse_Blockquote_ProducesQuoteBlock()
    {
        var doc = MarkdownPipeline.Parse("> This is a quote");

        doc.OfType<QuoteBlock>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Parse_NestedBlockquote_ProducesNestedQuoteBlocks()
    {
        var doc = MarkdownPipeline.Parse("> outer\n>> inner");

        var outerQuote = doc.OfType<QuoteBlock>().ShouldHaveSingleItem();
        outerQuote.Descendants<QuoteBlock>().Count().ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Parse_UnorderedList_ProducesListBlock()
    {
        var doc = MarkdownPipeline.Parse("- item one\n- item two\n- item three");

        var list = doc.OfType<ListBlock>().ShouldHaveSingleItem();
        list.IsOrdered.ShouldBeFalse();
        list.Count.ShouldBe(3);
    }

    [Fact]
    public void Parse_OrderedList_ProducesOrderedListBlock()
    {
        var doc = MarkdownPipeline.Parse("1. first\n2. second\n3. third");

        var list = doc.OfType<ListBlock>().ShouldHaveSingleItem();
        list.IsOrdered.ShouldBeTrue();
        list.Count.ShouldBe(3);
    }

    [Fact]
    public void Parse_EmphasisAndStrong_ProducesEmphasisInlines()
    {
        var doc = MarkdownPipeline.Parse("This is *italic* and **bold** text.");

        var emphases = doc.Descendants<EmphasisInline>().ToList();
        emphases.Count.ShouldBe(2);
    }

    [Fact]
    public void Parse_Strikethrough_ProducesEmphasisInlineWithTildeDelimiter()
    {
        var doc = MarkdownPipeline.Parse("This is ~~deleted~~ text.");

        var strikethrough = doc.Descendants<EmphasisInline>().Where(e => e.DelimiterChar == '~').ShouldHaveSingleItem();
        strikethrough.DelimiterCount.ShouldBe(2);
    }

    [Fact]
    public void Parse_ThematicBreak_ProducesThematicBreakBlock()
    {
        var doc = MarkdownPipeline.Parse("Above\n\n---\n\nBelow");

        doc.OfType<ThematicBreakBlock>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Parse_InlineLink_ProducesLinkInline()
    {
        var doc = MarkdownPipeline.Parse("[click here](https://example.com)");

        var link = doc.Descendants<LinkInline>().ShouldHaveSingleItem();
        link.Url.ShouldBe("https://example.com");
        link.IsAutoLink.ShouldBeFalse();
    }

    [Fact]
    public void Parse_Image_ProducesLinkInlineWithIsImage()
    {
        var doc = MarkdownPipeline.Parse("![alt text](image.png)");

        var image = doc.Descendants<LinkInline>().ShouldHaveSingleItem();
        image.IsImage.ShouldBeTrue();
        image.Url.ShouldBe("image.png");
    }

    [Fact]
    public void Parse_InlineCode_ProducesCodeInline()
    {
        var doc = MarkdownPipeline.Parse("Use `Console.WriteLine()` to print.");

        doc.Descendants<CodeInline>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Parse_CodeBlockInsideBlockquote_ProducesBothBlocks()
    {
        var doc = MarkdownPipeline.Parse("> ```cs\n> var x = 1;\n> ```");

        doc.OfType<QuoteBlock>().ShouldHaveSingleItem();
        doc.Descendants<FencedCodeBlock>().Count().ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Parse_ComplexDocument_ProducesBlocksInCorrectOrder()
    {
        var markdown =
            "---\ntitle: Test\n---\n\n# Title\n\nSome text with $x = 1$ math.\n\n```mermaid\ngraph TD;\n```\n\n> A quote\n\n- item\n";
        var doc = MarkdownPipeline.Parse(markdown);

        // Verify exact block types in document order
        var types = doc.Select(b => b.GetType()).ToList();
        var yamlIndex = types.IndexOf(typeof(YamlFrontMatterBlock));
        var headingIndex = types.IndexOf(typeof(HeadingBlock));
        var paragraphIndex = types.IndexOf(typeof(ParagraphBlock));
        var codeIndex = types.IndexOf(typeof(FencedCodeBlock));
        var quoteIndex = types.IndexOf(typeof(QuoteBlock));
        var listIndex = types.IndexOf(typeof(ListBlock));

        yamlIndex.ShouldBe(0);
        headingIndex.ShouldBeGreaterThan(yamlIndex);
        paragraphIndex.ShouldBeGreaterThan(headingIndex);
        codeIndex.ShouldBeGreaterThan(paragraphIndex);
        quoteIndex.ShouldBeGreaterThan(codeIndex);
        listIndex.ShouldBeGreaterThan(quoteIndex);

        doc.OfType<FencedCodeBlock>().ShouldHaveSingleItem().Info.ShouldBe("mermaid");
        doc.Descendants<MathInline>().ShouldHaveSingleItem().Content.ToString().ShouldBe("x = 1");
    }

    [Fact]
    public void Parse_MermaidBlock_PreservesContent()
    {
        var mermaidContent = "sequenceDiagram\n    Alice->>Bob: Hello\n    Bob-->>Alice: Hi";
        var doc = MarkdownPipeline.Parse($"```mermaid\n{mermaidContent}\n```");

        var block = doc.OfType<FencedCodeBlock>().ShouldHaveSingleItem();
        block.Info.ShouldBe("mermaid");
        var content = block.Lines.ToString();
        content.ShouldContain("Alice->>Bob");
        content.ShouldContain("Bob-->>Alice");
    }

    [Fact]
    public void Parse_HtmlEntityInParagraph_ProducesHtmlEntityInline()
    {
        var doc = MarkdownPipeline.Parse("Use &amp; for ampersand.");

        doc.Descendants<HtmlEntityInline>().ShouldHaveSingleItem();
    }
}
