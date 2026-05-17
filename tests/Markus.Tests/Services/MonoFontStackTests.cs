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
        string[] expected =
        [
            "Fira Code",
            "Iosevka",
            "JetBrains Mono",
            "Cascadia Code",
            "Consolas",
            "Menlo",
            "monospace",
        ];
        parts.ShouldBe(expected);
    }

    [Fact]
    public void Build_DoesNotDedupeWhenRequestedMatchesFirstFallback()
    {
        var result = MonoFontStack.Build("Iosevka");

        result.ShouldBe("Iosevka,Iosevka,JetBrains Mono,Cascadia Code,Consolas,Menlo,monospace");
    }

    [Fact]
    public void Build_EmptyRequested_ProducesLeadingComma()
    {
        var result = MonoFontStack.Build(string.Empty);

        result.ShouldBe(",Iosevka,JetBrains Mono,Cascadia Code,Consolas,Menlo,monospace");
    }

    [Fact]
    public void Build_WhitespaceRequested_IsPreservedVerbatim()
    {
        var result = MonoFontStack.Build("   ");

        result.ShouldBe("   ,Iosevka,JetBrains Mono,Cascadia Code,Consolas,Menlo,monospace");
    }
}
