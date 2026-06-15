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
}
