using Markus.Models;
using Markus.Services;

namespace Markus.Tests.Services;

public sealed class OutlineBuilderTests
{
    [Fact]
    public void Build_EmptyDocument_ReturnsEmpty()
    {
        var document = MarkdownPipeline.Parse(string.Empty);

        var outline = OutlineBuilder.Build(document);

        outline.ShouldBeEmpty();
    }

    [Fact]
    public void Build_NoHeadings_ReturnsEmpty()
    {
        var document = MarkdownPipeline.Parse("Just a paragraph.\n\nAnother paragraph with **bold**.");

        var outline = OutlineBuilder.Build(document);

        outline.ShouldBeEmpty();
    }

    [Fact]
    public void Build_SingleH1_ReturnsOneRoot()
    {
        var document = MarkdownPipeline.Parse("# Hello world");

        var outline = OutlineBuilder.Build(document);

        outline.Count.ShouldBe(1);
        var root = outline[0];
        root.Level.ShouldBe(1);
        root.Text.ShouldBe("Hello world");
        root.SourceLine.ShouldBe(0);
        root.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Build_SingleH1_SourceLineReflectsZeroBasedOffset()
    {
        var document = MarkdownPipeline.Parse("\n\n# Heading on line three");

        var outline = OutlineBuilder.Build(document);

        outline.Count.ShouldBe(1);
        outline[0].SourceLine.ShouldBe(2);
    }

    [Fact]
    public void Build_H1ThenH2ThenH3_NestsThreeDeep()
    {
        var document = MarkdownPipeline.Parse("# One\n\n## Two\n\n### Three");

        var outline = OutlineBuilder.Build(document);

        outline.Count.ShouldBe(1);
        var h1 = outline[0];
        h1.Level.ShouldBe(1);
        h1.Text.ShouldBe("One");
        h1.Children.Count.ShouldBe(1);

        var h2 = h1.Children[0];
        h2.Level.ShouldBe(2);
        h2.Text.ShouldBe("Two");
        h2.Children.Count.ShouldBe(1);

        var h3 = h2.Children[0];
        h3.Level.ShouldBe(3);
        h3.Text.ShouldBe("Three");
        h3.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Build_TwoH1Siblings_ReturnsTwoRoots()
    {
        var document = MarkdownPipeline.Parse("# First\n\n# Second");

        var outline = OutlineBuilder.Build(document);

        outline.Count.ShouldBe(2);
        outline[0].Level.ShouldBe(1);
        outline[0].Text.ShouldBe("First");
        outline[0].Children.ShouldBeEmpty();
        outline[1].Level.ShouldBe(1);
        outline[1].Text.ShouldBe("Second");
        outline[1].Children.ShouldBeEmpty();
    }

    [Fact]
    public void Build_H1H2H1_SecondH1IsSiblingNotChild()
    {
        var document = MarkdownPipeline.Parse("# Alpha\n\n## Alpha-Sub\n\n# Beta");

        var outline = OutlineBuilder.Build(document);

        outline.Count.ShouldBe(2);

        var alpha = outline[0];
        alpha.Level.ShouldBe(1);
        alpha.Text.ShouldBe("Alpha");
        alpha.Children.Count.ShouldBe(1);
        alpha.Children[0].Level.ShouldBe(2);
        alpha.Children[0].Text.ShouldBe("Alpha-Sub");

        var beta = outline[1];
        beta.Level.ShouldBe(1);
        beta.Text.ShouldBe("Beta");
        beta.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Build_H2ThenH1_BothBecomeRoots()
    {
        var document = MarkdownPipeline.Parse("## Lower First\n\n# Higher Second");

        var outline = OutlineBuilder.Build(document);

        outline.Count.ShouldBe(2);
        outline[0].Level.ShouldBe(2);
        outline[0].Text.ShouldBe("Lower First");
        outline[0].Children.ShouldBeEmpty();
        outline[1].Level.ShouldBe(1);
        outline[1].Text.ShouldBe("Higher Second");
        outline[1].Children.ShouldBeEmpty();
    }

    [Fact]
    public void Build_HeadingWithInlineFormatting_FlattensToPlainText()
    {
        var document = MarkdownPipeline.Parse("# Hello **bold** _italic_");

        var outline = OutlineBuilder.Build(document);

        outline.Count.ShouldBe(1);
        outline[0].Text.ShouldBe("Hello bold italic");
    }

    [Fact]
    public void Build_HeadingWithInlineCode_IncludesCodeText()
    {
        var document = MarkdownPipeline.Parse("# Hello `code`");

        var outline = OutlineBuilder.Build(document);

        outline.Count.ShouldBe(1);
        outline[0].Text.ShouldContain("Hello");
        outline[0].Text.ShouldContain("code");
    }

    [Fact]
    public void Build_HeadingWithAutolink_IncludesUrl()
    {
        var document = MarkdownPipeline.Parse("# Link <https://example.com>");

        var outline = OutlineBuilder.Build(document);

        outline.Count.ShouldBe(1);
        outline[0].Text.ShouldContain("Link");
        outline[0].Text.ShouldContain("https://example.com");
    }

    [Fact]
    public void Build_HeadingWithTrailingWhitespace_IsTrimmed()
    {
        var document = MarkdownPipeline.Parse("#    Padded   ");

        var outline = OutlineBuilder.Build(document);

        outline.Count.ShouldBe(1);
        outline[0].Text.ShouldBe("Padded");
    }

    [Fact]
    public void Build_DeepNestingThenPopBack_PlacesNodesAtCorrectLevels()
    {
        var document = MarkdownPipeline.Parse("# A\n\n## A1\n\n### A1a\n\n## A2\n\n# B");

        var outline = OutlineBuilder.Build(document);

        outline.Count.ShouldBe(2);

        var a = outline[0];
        a.Text.ShouldBe("A");
        a.Children.Count.ShouldBe(2);
        a.Children[0].Text.ShouldBe("A1");
        a.Children[0].Children.Count.ShouldBe(1);
        a.Children[0].Children[0].Text.ShouldBe("A1a");
        a.Children[1].Text.ShouldBe("A2");
        a.Children[1].Children.ShouldBeEmpty();

        var b = outline[1];
        b.Text.ShouldBe("B");
        b.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Build_SiblingSourceLines_MatchHeadingPositions()
    {
        var document = MarkdownPipeline.Parse("# First\n\n# Second\n\n# Third");

        var outline = OutlineBuilder.Build(document);

        outline.Count.ShouldBe(3);
        outline[0].SourceLine.ShouldBe(0);
        outline[1].SourceLine.ShouldBe(2);
        outline[2].SourceLine.ShouldBe(4);
    }

    [Fact]
    public void Build_ReturnsListType_ThatExposesIndexer()
    {
        var document = MarkdownPipeline.Parse("# Only");

        var outline = OutlineBuilder.Build(document);

        outline.ShouldBeAssignableTo<IReadOnlyList<OutlineNode>>();
        outline[0].ShouldNotBeNull();
    }
}
