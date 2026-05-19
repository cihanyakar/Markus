using Markus.Services;

namespace Markus.Tests.Services;

public sealed class MarkdownTableFormatterTests
{
    [Fact]
    public void Empty_Input_ReturnsEmpty()
    {
        MarkdownTableFormatter.Format(string.Empty).ShouldBe(string.Empty);
    }

    [Fact]
    public void NoTable_PassesThrough()
    {
        var source = "# Heading\n\nJust a paragraph with **bold** text.\n";
        MarkdownTableFormatter.Format(source).ShouldBe(source);
    }

    [Fact]
    public void Aligns_Columns_To_Widest_Cell()
    {
        var input = "| a | bbb |\n|---|---|\n| 1 | xx |\n";
        var expected = "| a | bbb |\n|---|-----|\n| 1 | xx  |\n";

        MarkdownTableFormatter.Format(input).ShouldBe(expected);
    }

    [Fact]
    public void Already_Aligned_Table_Round_Trips()
    {
        var input = "| Name | Score |\n|------|-------|\n| Ana  | 12    |\n| Bo   | 5     |\n";

        MarkdownTableFormatter.Format(input).ShouldBe(input);
    }

    [Fact]
    public void Preserves_Left_Alignment_Delimiter()
    {
        var input = "| a | b |\n|:--|---|\n| 1 | 2 |\n";

        var formatted = MarkdownTableFormatter.Format(input);

        formatted.ShouldContain(":---");
    }

    [Fact]
    public void Preserves_Right_Alignment_Delimiter()
    {
        var input = "| a | b |\n|---|--:|\n| 1 | 2 |\n";

        var formatted = MarkdownTableFormatter.Format(input);

        formatted.ShouldContain("---:");
    }

    [Fact]
    public void Preserves_Center_Alignment_Delimiter()
    {
        var input = "| a | b |\n|:-:|---|\n| 1 | 2 |\n";

        var formatted = MarkdownTableFormatter.Format(input);

        formatted.ShouldContain(":---:");
    }

    [Fact]
    public void Right_Aligned_Numbers_Pad_On_The_Left()
    {
        var input = "| Name | Score |\n|------|------:|\n| Ana  | 12 |\n| Boris | 5 |\n";

        var formatted = MarkdownTableFormatter.Format(input);

        // The "Score" column is right-aligned; numbers should sit at the far
        // right of the cell, padded with leading spaces.
        formatted.ShouldContain("|    12 |");
        formatted.ShouldContain("|     5 |");
    }

    [Fact]
    public void Pads_Short_Rows_With_Empty_Cells()
    {
        var input = "| a | b | c |\n|---|---|---|\n| 1 | 2 |\n";

        var formatted = MarkdownTableFormatter.Format(input);

        // Missing third cell on the data row gets filled with an empty cell.
        formatted.ShouldContain("| 1 | 2 |   |");
    }

    [Fact]
    public void Two_Tables_Separated_By_Paragraph_Are_Both_Formatted()
    {
        var input =
            "| a | bbb |\n|---|---|\n| 1 | x |\n"
            + "\n"
            + "between\n"
            + "\n"
            + "| name | s |\n|---|---|\n| Ali | 10 |\n";

        var formatted = MarkdownTableFormatter.Format(input);

        formatted.ShouldContain("| 1 | x   |");
        formatted.ShouldContain("| Ali  | 10 |");
        formatted.ShouldContain("between");
    }

    [Fact]
    public void Single_Column_Table_Aligns()
    {
        var input = "| Header |\n|---|\n| short |\n| longer entry |\n";

        var formatted = MarkdownTableFormatter.Format(input);

        formatted.ShouldContain("| Header       |");
        formatted.ShouldContain("| short        |");
        formatted.ShouldContain("| longer entry |");
    }

    [Fact]
    public void CRLF_Line_Endings_Normalized()
    {
        var input = "| a | b |\r\n|---|---|\r\n| 1 | 22 |\r\n";

        var formatted = MarkdownTableFormatter.Format(input);

        // Output uses LF; the table aligns regardless.
        formatted.ShouldContain("| 1 | 22 |");
        formatted.ShouldNotContain("\r\n");
    }

    [Fact]
    public void Non_Table_Pipes_Are_Not_Treated_As_Tables()
    {
        // A single line with pipes but no delimiter row underneath is just
        // text and should pass through untouched.
        var input = "Look at | this | text\n";

        MarkdownTableFormatter.Format(input).ShouldBe(input);
    }
}
