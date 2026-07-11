using System.Text;
using Avalonia.Controls;
using Markus.Rendering;
using Markus.Services;
using Markus.Views;

namespace Markus.Tests.Rendering;

// A markdown document is untrusted content. A tiny document can parse to a table
// with an enormous number of columns or cells, which would build an unbounded,
// unvirtualized Grid and freeze the UI or exhaust memory. These tests pin the
// threshold decision and the placeholder fallback, and the mermaid control's
// initial-state decision when the renderer binary is not available.
public sealed class RenderDosProtectionTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    [InlineData(MarkdownRenderer.MaxTableColumns, 1)]
    [InlineData(50, 50)]
    public void IsTableTooLarge_WithinLimits_IsFalse(int columns, int rows)
    {
        MarkdownRenderer.IsTableTooLarge(columns, rows).ShouldBeFalse();
    }

    [Fact]
    public void IsTableTooLarge_TooManyColumns_IsTrue()
    {
        MarkdownRenderer.IsTableTooLarge(MarkdownRenderer.MaxTableColumns + 1, 1).ShouldBeTrue();
    }

    [Fact]
    public void IsTableTooLarge_TooManyCells_IsTrue()
    {
        // Column count alone is under the column cap, but columns * rows exceeds
        // the total-cell cap.
        var columns = MarkdownRenderer.MaxTableColumns / 2;
        var rows = (MarkdownRenderer.MaxTableCells / columns) + 2;

        MarkdownRenderer.IsTableTooLarge(columns, rows).ShouldBeTrue();
    }

    [Fact]
    public void IsTableTooLarge_HugeDimensions_DoesNotOverflow()
    {
        // A naive int multiply of two large dimensions would overflow to a small
        // or negative number and wrongly report "not too large".
        MarkdownRenderer.IsTableTooLarge(int.MaxValue, int.MaxValue).ShouldBeTrue();
    }

    [Fact]
    public void Table_WithinLimits_RendersAsGrid()
    {
        var blocks = Render(BuildTableMarkdown(columns: 4, bodyRows: 2));

        blocks[0].Control.ShouldBeOfType<Grid>();
    }

    [Fact]
    public void Table_TooWide_RendersPlaceholderNotGrid()
    {
        var blocks = Render(BuildTableMarkdown(columns: MarkdownRenderer.MaxTableColumns + 5, bodyRows: 1));

        // A Border placeholder, never a Grid: building the giant Grid is exactly
        // what the guard prevents.
        blocks[0].Control.ShouldBeOfType<Border>();
        blocks.ShouldNotContain(b => b.Control is Grid);
    }

    [Fact]
    public void DecideInitialState_WhenAvailable_IsLoading()
    {
        MermaidControl.DecideInitialState(rendererAvailable: true).ShouldBe(MermaidControl.InitialState.Loading);
    }

    [Fact]
    public void DecideInitialState_WhenUnavailable_IsUnavailable()
    {
        MermaidControl.DecideInitialState(rendererAvailable: false).ShouldBe(MermaidControl.InitialState.Unavailable);
    }

    [Fact]
    public void HasPendingRender_SameTextSameGeneration_IsFalse()
    {
        MarkdownPreviewControl.HasPendingRender("doc", "doc", generation: 3, lastRenderedGeneration: 3).ShouldBeFalse();
    }

    [Fact]
    public void HasPendingRender_ChangedText_IsTrue()
    {
        MarkdownPreviewControl.HasPendingRender("new", "old", generation: 1, lastRenderedGeneration: 1).ShouldBeTrue();
    }

    [Fact]
    public void HasPendingRender_ChangedGeneration_IsTrue()
    {
        // A forced re-render (theme/font change) bumps the generation while the
        // text is unchanged; the loop must still run.
        MarkdownPreviewControl
            .HasPendingRender("doc", "doc", generation: 2, lastRenderedGeneration: 1)
            .ShouldBeTrue();
    }

    [Fact]
    public void HasPendingRender_AfterFailure_RetriesEvenWithIdenticalText()
    {
        // After a render error the control sets the last-rendered generation to
        // the FailedGeneration sentinel. Even if the very next scheduled source is
        // byte-for-byte identical, the loop must re-attempt so the preview can
        // recover from the frozen error state.
        MarkdownPreviewControl
            .HasPendingRender(
                "doc",
                "doc",
                generation: 5,
                lastRenderedGeneration: MarkdownPreviewControl.FailedGeneration
            )
            .ShouldBeTrue();
    }

    private static List<RenderedBlock> Render(string markdown)
    {
        return MarkdownRenderer.Render(MarkdownPipeline.Parse(markdown)).ToList();
    }

    // Builds a GitHub-flavored markdown pipe table with the requested number of
    // columns and body rows (plus the header and delimiter rows).
    private static string BuildTableMarkdown(int columns, int bodyRows)
    {
        var sb = new StringBuilder();
        AppendRow(sb, columns, cellIndex => "h" + cellIndex);
        AppendRow(sb, columns, _ => "---");
        for (var r = 0; r < bodyRows; r++)
        {
            AppendRow(sb, columns, cellIndex => "c" + cellIndex);
        }
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, int columns, Func<int, string> cell)
    {
        sb.Append('|');
        for (var c = 0; c < columns; c++)
        {
            sb.Append(' ').Append(cell(c)).Append(" |");
        }
        sb.Append('\n');
    }
}
