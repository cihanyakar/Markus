using Markus.Services;

namespace Markus.Tests.Services;

public sealed class MonoFontStackTests
{
    [Fact]
    public void Build_PutsRequestedFontFirst()
    {
        var result = MonoFontStack.Build("Fira Code");

        result.ShouldStartWith("Fira Code,");
    }

    [Fact]
    public void Build_ContainsAllFallbacksInOrder()
    {
        var result = MonoFontStack.Build("Fira Code");

        var parts = result.Split(',');
        string[] expected = ["Fira Code", "Menlo", "Consolas", "ui-monospace", "monospace"];
        parts.ShouldBe(expected);
    }

    [Fact]
    public void Build_DoesNotDedupeWhenRequestedMatchesFirstFallback()
    {
        var result = MonoFontStack.Build("Menlo");

        result.ShouldBe("Menlo,Menlo,Consolas,ui-monospace,monospace");
    }

    [Fact]
    public void Build_EmptyRequested_ProducesLeadingComma()
    {
        var result = MonoFontStack.Build(string.Empty);

        result.ShouldBe(",Menlo,Consolas,ui-monospace,monospace");
    }

    [Fact]
    public void Build_WhitespaceRequested_IsPreservedVerbatim()
    {
        var result = MonoFontStack.Build("   ");

        result.ShouldBe("   ,Menlo,Consolas,ui-monospace,monospace");
    }
}
