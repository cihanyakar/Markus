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
    public void Parse_InlineMath_ProducesMathInline()
    {
        var doc = MarkdownPipeline.Parse("$x = 1$");

        doc.Descendants<MathInline>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Parse_BlockMath_ProducesMathBlock()
    {
        var doc = MarkdownPipeline.Parse("$$\nx = 1\n$$");

        doc.OfType<MathBlock>().ShouldHaveSingleItem();
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
}
