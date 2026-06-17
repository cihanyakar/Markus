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

    [Fact]
    public void Escaped_Pipes_Inside_Cells_Are_Preserved()
    {
        // Bug: SplitCells uses a naive Split('|') that treats escaped
        // pipes (\|) as column delimiters. A cell like "a \| b" should
        // remain a single cell, but the naive split produces three cells.
        var input =
            @"| Name | Expression |
|------|------------|
| test | a \| b     |
";

        var formatted = MarkdownTableFormatter.Format(input);

        // The escaped pipe should stay inside its cell, and the table
        // should remain two columns wide.
        var dataLines = formatted.Split('\n').Where(l => l.Contains("test", StringComparison.Ordinal)).ToList();
        dataLines.Count.ShouldBe(1);
        // Count unescaped pipe delimiters on the data row. A proper 2-column
        // row has exactly 3 real (unescaped) pipes: leading, middle, trailing.
        // The escaped \| inside "a \| b" should NOT be counted.
        var row = dataLines[0];
        var unescapedPipes = 0;
        for (var i = 0; i < row.Length; i++)
        {
            if (row[i] == '|' && (i == 0 || row[i - 1] != '\\'))
            {
                unescapedPipes++;
            }
        }
        // With the bug, SplitCells sees 3 cells ("Name", "a \", " b")
        // which inflates the column count, so unescapedPipes > 3.
        unescapedPipes.ShouldBe(3);
    }
}
