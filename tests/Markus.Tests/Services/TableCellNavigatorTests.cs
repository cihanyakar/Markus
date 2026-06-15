using Markus.Services;

namespace Markus.Tests.Services;

public sealed class TableCellNavigatorTests
{
    [Fact]
    public void TryFindTableAt_Cursor_In_Simple_Table_Returns_Region()
    {
        var source = "| a | b |\n|---|---|\n| 1 | 2 |\n";
        // Offset is inside the data row "| 1 | 2 |", which starts at index 18.
        var cursor = 20;

        var found = TableCellNavigator.TryFindTableAt(source, cursor, out var region);

        found.ShouldBeTrue();
        region.ShouldNotBeNull();
        region.HeaderLine.ShouldBe(0);
        region.DelimiterLine.ShouldBe(1);
        region.StartLine.ShouldBe(0);
        region.EndLine.ShouldBe(2);
        region.Rows.Count.ShouldBe(3);
        region.Rows[0].Count.ShouldBe(2);
    }

    [Fact]
    public void TryFindTableAt_Cursor_In_Paragraph_Returns_False()
    {
        var source = "Just a plain paragraph with no table at all.\n";
        var cursor = 10;

        var found = TableCellNavigator.TryFindTableAt(source, cursor, out _);

        found.ShouldBeFalse();
    }

    [Fact]
    public void TryFindTableAt_Empty_Source_Returns_False()
    {
        TableCellNavigator.TryFindTableAt(string.Empty, 0, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryFindTableAt_Cursor_On_Heading_Above_Table_Returns_False()
    {
        var source = "# Title\n\n| a | b |\n|---|---|\n| 1 | 2 |\n";
        // Cursor is on the heading line, before the blank line and table.
        var cursor = 3;

        TableCellNavigator.TryFindTableAt(source, cursor, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryFindTableAt_Two_Tables_Selects_The_One_Containing_Cursor()
    {
        var source =
            "| a | b |\n|---|---|\n| 1 | 2 |\n" + "\n" + "between\n" + "\n" + "| x | y |\n|---|---|\n| 9 | 8 |\n";
        // "| x | y |" begins at offset 39; cursor on the data row of the 2nd table.
        var cursor = source.IndexOf("| 9", StringComparison.Ordinal) + 2;

        var found = TableCellNavigator.TryFindTableAt(source, cursor, out var region);

        found.ShouldBeTrue();
        region.ShouldNotBeNull();
        region.HeaderLine.ShouldBe(6);
        region.Rows[0][0].Length.ShouldBe(3); // " x ", trimmed elsewhere
    }

    [Fact]
    public void TryFindTableAt_Cursor_In_Paragraph_Between_Two_Tables_Returns_False()
    {
        var source =
            "| a | b |\n|---|---|\n| 1 | 2 |\n" + "\n" + "between\n" + "\n" + "| x | y |\n|---|---|\n| 9 | 8 |\n";
        var cursor = source.IndexOf("between", StringComparison.Ordinal) + 2;

        TableCellNavigator.TryFindTableAt(source, cursor, out _).ShouldBeFalse();
    }

    [Fact]
    public void NextCell_Forward_Within_Row_Returns_Next_Cell()
    {
        var source = "| a | b | c |\n|---|---|---|\n| 1 | 2 | 3 |\n";
        TableCellNavigator.TryFindTableAt(source, 2, out var region).ShouldBeTrue();
        region.ShouldNotBeNull();
        // Caret inside cell 0 of header row ("| a |"), offset 2.
        var next = TableCellNavigator.NextCell(region, currentOffset: 2, forward: true);

        next.ShouldNotBeNull();
        next!.Value.Offset.ShouldBe(region.Rows[0][1].Offset);
    }

    [Fact]
    public void NextCell_Forward_From_Last_Cell_Skips_Delimiter_Row()
    {
        var source = "| a | b |\n|---|---|\n| 1 | 2 |\n";
        TableCellNavigator.TryFindTableAt(source, 2, out var region).ShouldBeTrue();
        region.ShouldNotBeNull();
        var lastHeaderCell = region.Rows[0][1].Offset;

        var next = TableCellNavigator.NextCell(region, currentOffset: lastHeaderCell, forward: true);

        // Skip the delimiter row entirely; land in the first cell of the data row.
        next.ShouldNotBeNull();
        next!.Value.Offset.ShouldBe(region.Rows[2][0].Offset);
    }

    [Fact]
    public void NextCell_Forward_From_Last_Cell_Of_Last_Row_Returns_Null()
    {
        var source = "| a | b |\n|---|---|\n| 1 | 2 |\n";
        TableCellNavigator.TryFindTableAt(source, 2, out var region).ShouldBeTrue();
        region.ShouldNotBeNull();
        var lastCell = region.Rows[2][1].Offset;

        var next = TableCellNavigator.NextCell(region, currentOffset: lastCell, forward: true);

        next.ShouldBeNull();
    }
}
