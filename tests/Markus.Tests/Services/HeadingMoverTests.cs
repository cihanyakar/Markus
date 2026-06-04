using Markus.Models;
using Markus.Services;

namespace Markus.Tests.Services;

public sealed class HeadingMoverTests
{
    [Fact]
    public void Empty_Source_Returns_Empty()
    {
        var outline = Array.Empty<OutlineNode>();

        HeadingMover
            .Move(string.Empty, outline, draggedLine: 1, targetLine: -1, DropPosition.After)
            .ShouldBe(string.Empty);
    }

    [Fact]
    public void Move_Section_After_Sibling()
    {
        var source = "# A\nbody of A\n\n# B\nbody of B\n\n# C\nbody of C";
        var outline = BuildOutline(source);

        // Move "A" so it sits after "B".
        var moved = HeadingMover.Move(
            source,
            outline,
            draggedLine: outline[0].SourceLine,
            targetLine: outline[1].SourceLine,
            DropPosition.After
        );

        var headings = moved.Split('\n').Where(l => l.StartsWith('#')).ToList();
        headings.ShouldBe(["# B", "# A", "# C"]);
    }

    [Fact]
    public void Move_Section_Before_Sibling()
    {
        var source = "# A\nbody A\n\n# B\nbody B\n\n# C\nbody C";
        var outline = BuildOutline(source);

        // Drop C before A.
        var moved = HeadingMover.Move(
            source,
            outline,
            draggedLine: outline[2].SourceLine,
            targetLine: outline[0].SourceLine,
            DropPosition.Before
        );

        var headings = moved.Split('\n').Where(l => l.StartsWith('#')).ToList();
        headings.ShouldBe(["# C", "# A", "# B"]);
    }

    [Fact]
    public void Moving_Heading_Brings_Its_Body_Along()
    {
        var source = "# A\nline a1\nline a2\n\n# B\nline b1";
        var outline = BuildOutline(source);

        var moved = HeadingMover.Move(
            source,
            outline,
            draggedLine: outline[0].SourceLine,
            targetLine: outline[1].SourceLine,
            DropPosition.After
        );

        // After move, B should come first, then A's full body, all preserved.
        moved.ShouldContain("line a1");
        moved.ShouldContain("line a2");
        moved.ShouldContain("line b1");
        // B precedes A in the output.
        moved
            .IndexOf("# B", StringComparison.Ordinal)
            .ShouldBeLessThan(moved.IndexOf("# A", StringComparison.Ordinal));
    }

    [Fact]
    public void Moving_Parent_Heading_Includes_Children()
    {
        var source = "# A\nbody A\n\n## A.1\nchild 1\n\n# B\nbody B";
        var outline = BuildOutline(source);

        // outline[0] = A, outline[1] = B (siblings at root). A.1 is nested
        // under A, so we don't see it at the root index here. Drop A after B,
        // and A.1 must follow A.
        var moved = HeadingMover.Move(
            source,
            outline,
            draggedLine: outline[0].SourceLine,
            targetLine: outline[1].SourceLine,
            DropPosition.After
        );

        var idxB = moved.IndexOf("# B", StringComparison.Ordinal);
        var idxA = moved.IndexOf("# A\n", StringComparison.Ordinal);
        var idxChild = moved.IndexOf("## A.1", StringComparison.Ordinal);

        idxB.ShouldBeLessThan(idxA);
        idxA.ShouldBeLessThan(idxChild);
    }

    [Fact]
    public void Drop_Inside_Becomes_First_Line_After_Target_Heading()
    {
        var source = "# A\nbody A\n\n# B\nbody B";
        var outline = BuildOutline(source);

        // Drop B "inside" A. B should land right after A's heading line.
        var moved = HeadingMover.Move(
            source,
            outline,
            draggedLine: outline[1].SourceLine,
            targetLine: outline[0].SourceLine,
            DropPosition.Inside
        );

        var lines = moved.Split('\n');
        // Line 0 = "# A", line 1 should be "# B" (dropped right under A).
        lines[0].ShouldBe("# A");
        lines[1].ShouldBe("# B");
    }

    [Fact]
    public void Negative_Target_Moves_To_End()
    {
        var source = "# A\nbody A\n\n# B\nbody B";
        var outline = BuildOutline(source);

        // -1 is the sentinel for "no target → append at end of document".
        // 0 is a real Markdig line, so it can't double as the sentinel.
        var moved = HeadingMover.Move(
            source,
            outline,
            draggedLine: outline[0].SourceLine,
            targetLine: -1,
            DropPosition.After
        );

        var headings = moved.Split('\n').Where(l => l.StartsWith('#')).ToList();
        headings.ShouldBe(["# B", "# A"]);
    }

    [Fact]
    public void Unknown_Dragged_Line_Is_NoOp()
    {
        var source = "# A\nbody A\n";
        var outline = BuildOutline(source);

        HeadingMover
            .Move(source, outline, draggedLine: 999, targetLine: outline[0].SourceLine, DropPosition.After)
            .ShouldBe(source);
    }

    [Fact]
    public void Move_First_Section_Before_Last_Of_Three_Siblings()
    {
        // Bug: FindHeadingByMatch scans from i=0 and returns the FIRST line
        // whose prefix matches the target level. When moving A before C in a
        // three-sibling document, it incorrectly resolves the target to B
        // (the first "# " match in remaining) instead of C, so A lands
        // before B instead of before C.
        var source = "# A\nbody A\n\n# B\nbody B\n\n# C\nbody C";
        var outline = BuildOutline(source);

        // Drag A to sit before C.
        var moved = HeadingMover.Move(
            source,
            outline,
            draggedLine: outline[0].SourceLine,
            targetLine: outline[2].SourceLine,
            DropPosition.Before
        );

        var headings = moved.Split('\n').Where(l => l.StartsWith('#')).ToList();
        // Expected: B, A, C. The bug produces A, B, C (no visible move)
        // because adjustedTargetLine points at B instead of C.
        headings.ShouldBe(["# B", "# A", "# C"]);
    }

    private static IReadOnlyList<OutlineNode> BuildOutline(string source)
    {
        // OutlineBuilder.Build is the same producer the live VM uses, so the
        // line numbers stay consistent with the runtime drag-drop flow.
        var doc = MarkdownPipeline.Parse(source);
        return OutlineBuilder.Build(doc);
    }
}
