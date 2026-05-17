using Markus.Services;

namespace Markus.Tests.Services;

public sealed class TextSearchMathTests
{
    [Fact]
    public void CountMatches_EmptyTerm_ReturnsZero()
    {
        TextSearchMath.CountMatches("hello world", string.Empty, caseSensitive: false).ShouldBe(0);
    }

    [Fact]
    public void CountMatches_EmptyText_ReturnsZero()
    {
        TextSearchMath.CountMatches(string.Empty, "hello", caseSensitive: false).ShouldBe(0);
    }

    [Fact]
    public void CountMatches_SingleMatch_ReturnsOne()
    {
        TextSearchMath.CountMatches("the quick brown fox", "quick", caseSensitive: true).ShouldBe(1);
    }

    [Fact]
    public void CountMatches_MultipleNonOverlapping_CountsAll()
    {
        TextSearchMath.CountMatches("ab ab ab ab", "ab", caseSensitive: true).ShouldBe(4);
    }

    [Fact]
    public void CountMatches_OverlappingPattern_AdvancesByTermLength()
    {
        // "aa" in "aaaa": loop advances by term.Length (2), so finds at 0 and 2, not 0,1,2.
        TextSearchMath.CountMatches("aaaa", "aa", caseSensitive: true).ShouldBe(2);
    }

    [Theory]
    [InlineData("ABC abc ABC", "abc", true, 1)]
    [InlineData("ABC abc ABC", "abc", false, 3)]
    [InlineData("ABC abc ABC", "ABC", true, 2)]
    public void CountMatches_CaseSensitivity_FlipsTheCount(string text, string term, bool caseSensitive, int expected)
    {
        TextSearchMath.CountMatches(text, term, caseSensitive).ShouldBe(expected);
    }

    [Fact]
    public void CountMatches_TermLongerThanText_ReturnsZero()
    {
        TextSearchMath.CountMatches("hi", "hello world", caseSensitive: false).ShouldBe(0);
    }

    [Fact]
    public void CountMatches_UnicodeOrdinalMatch_TreatsBytesExactly()
    {
        // StringComparison.Ordinal compares code units exactly, so the same Unicode glyph matches.
        TextSearchMath.CountMatches("naive café café", "café", caseSensitive: true).ShouldBe(2);
    }

    [Fact]
    public void CountMatches_UnicodeOrdinal_DoesNotEquatePrecomposedAndDecomposed()
    {
        // Precomposed "é" (U+00E9) vs decomposed "e" + combining acute (U+0301) differ under Ordinal compare.
        var text = "café";
        var term = "café";
        TextSearchMath.CountMatches(text, term, caseSensitive: true).ShouldBe(0);
    }

    [Fact]
    public void CurrentMatchIndex_EmptyTerm_ReturnsMinusOne()
    {
        TextSearchMath.CurrentMatchIndex("hello", string.Empty, anchor: 0, caseSensitive: false).ShouldBe(-1);
    }

    [Fact]
    public void CurrentMatchIndex_NoMatches_ReturnsMinusOne()
    {
        TextSearchMath.CurrentMatchIndex("hello world", "zzz", anchor: 5, caseSensitive: true).ShouldBe(-1);
    }

    [Fact]
    public void CurrentMatchIndex_AnchorExactlyOnFirstMatch_ReturnsZero()
    {
        // "hello world hello", "hello" at idx 0 and idx 12; anchor=0 hits the first.
        TextSearchMath.CurrentMatchIndex("hello world hello", "hello", anchor: 0, caseSensitive: true).ShouldBe(0);
    }

    [Fact]
    public void CurrentMatchIndex_AnchorExactlyOnSecondMatch_ReturnsOne()
    {
        TextSearchMath.CurrentMatchIndex("hello world hello", "hello", anchor: 12, caseSensitive: true).ShouldBe(1);
    }

    [Fact]
    public void CurrentMatchIndex_AnchorBetweenMatches_ReturnsPriorMatch()
    {
        // matches at 0, 12; anchor=6 is between. Loop sees idx=0 (lastFound=0), then idx=12 > 6 with lastFound>=0, returns 0.
        TextSearchMath.CurrentMatchIndex("hello world hello", "hello", anchor: 6, caseSensitive: true).ShouldBe(0);
    }

    [Fact]
    public void CurrentMatchIndex_AnchorAfterLastMatch_ReturnsLastMatch()
    {
        // matches at 0, 12; anchor=200 is past the end. Loop walks all matches, exits, returns lastFound=1.
        TextSearchMath.CurrentMatchIndex("hello world hello", "hello", anchor: 200, caseSensitive: true).ShouldBe(1);
    }

    [Fact]
    public void CurrentMatchIndex_SingleMatchAnchorOnIt_ReturnsZero()
    {
        TextSearchMath.CurrentMatchIndex("xx hello xx", "hello", anchor: 3, caseSensitive: true).ShouldBe(0);
    }

    [Fact]
    public void CurrentMatchIndex_AnchorBeforeOnlyMatch_ReturnsThatMatch()
    {
        // First iteration: idx=3 > anchor=0 but lastFound is -1 so the early-return guard fails.
        // lastFound becomes 0, loop exits, returns 0.
        TextSearchMath.CurrentMatchIndex("xx hello xx", "hello", anchor: 0, caseSensitive: true).ShouldBe(0);
    }

    [Theory]
    [InlineData("ABC abc ABC", "abc", 4, true, 0)] // case-sensitive, only one match at idx=4, anchor on it
    [InlineData("ABC abc ABC", "abc", 4, false, 1)] // case-insensitive, matches at 0,4,8; anchor=4 hits index 1
    [InlineData("ABC abc ABC", "abc", 8, false, 2)] // case-insensitive, anchor on the third match
    public void CurrentMatchIndex_CaseSensitivity_ChangesResult(
        string text,
        string term,
        int anchor,
        bool caseSensitive,
        int expected
    )
    {
        TextSearchMath.CurrentMatchIndex(text, term, anchor, caseSensitive).ShouldBe(expected);
    }
}
