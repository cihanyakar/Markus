using Markus.Services;

namespace Markus.Tests.Services;

// GFM table cells escape a literal pipe as \| and a literal backslash as \\.
// Splitting must therefore count the run of backslashes before a pipe: an odd
// run escapes the pipe (keep it in the cell), an even run leaves the pipe as a
// real column separator.
public sealed class MarkdownTableFormatterSplitTests
{
    [Fact]
    public void EscapedPipe_StaysInOneCell()
    {
        // a\|b -> single cell, the pipe is escaped by one backslash.
        MarkdownTableFormatter.SplitCells(@"a\|b").ShouldBe([@"a\|b"]);
    }

    [Fact]
    public void EscapedBackslashThenPipe_SplitsIntoTwoCells()
    {
        // a\\|b -> the backslash is escaped, so the pipe is a real separator.
        MarkdownTableFormatter.SplitCells(@"a\\|b").ShouldBe([@"a\\", "b"]);
    }

    [Fact]
    public void ThreeBackslashesThenPipe_StaysInOneCell()
    {
        // a\\\|b -> escaped backslash then escaped pipe, one cell.
        MarkdownTableFormatter.SplitCells(@"a\\\|b").ShouldBe([@"a\\\|b"]);
    }

    [Fact]
    public void PlainPipes_SplitNormally()
    {
        MarkdownTableFormatter.SplitCells("a|b|c").ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public void LeadingAndTrailingPipes_AreStripped()
    {
        MarkdownTableFormatter.SplitCells("| a | b |").ShouldBe(["a", "b"]);
    }
}
