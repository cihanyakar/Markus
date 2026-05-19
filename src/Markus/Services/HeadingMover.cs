using Markus.Models;

namespace Markus.Services;

/// <summary>
/// Reorders ATX headings (and their bodies) inside a Markdown source string.
/// Used by the outline panel's drag-drop reorder: given an outline tree and
/// the source line numbers of dragged/target headings, produces a new source
/// string with the dragged section spliced into the new spot.
/// </summary>
internal static class HeadingMover
{
    public static string Move(
        string source,
        IReadOnlyList<OutlineNode> outline,
        int draggedLine,
        int targetLine,
        DropPosition position
    )
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }
        var lines = source.Replace("\r\n", "\n").Split('\n');
        // Collect every heading's (line, level) so we can compute body ranges.
        var headings = Flatten(outline);
        if (headings.Count == 0)
        {
            return source;
        }
        var draggedIndex = headings.FindIndex(h => h.Line == draggedLine);
        if (draggedIndex < 0)
        {
            return source;
        }
        var (srcStart, srcEnd) = ResolveRange(headings, draggedIndex, lines.Length);
        // Negative targetLine is the "no target → drop at end of doc" sentinel.
        // Zero is a real Markdig line index, so it must NOT be the sentinel.
        var targetIndex = targetLine < 0 ? -1 : headings.FindIndex(h => h.Line == targetLine);
        // Build the buffer of dragged lines (inclusive of both ends).
        var draggedBuffer = new List<string>(srcEnd - srcStart + 1);
        for (var i = srcStart; i <= srcEnd; i++)
        {
            draggedBuffer.Add(lines[i]);
        }
        // Remove dragged section from the original list.
        var remaining = new List<string>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            if (i < srcStart || i > srcEnd)
            {
                remaining.Add(lines[i]);
            }
        }
        var insertAt = ResolveInsertIndex(remaining, headings, targetIndex, position, srcStart);
        // Splice the dragged buffer back in at the resolved offset.
        var output = new List<string>(remaining.Count + draggedBuffer.Count);
        for (var i = 0; i < remaining.Count; i++)
        {
            if (i == insertAt)
            {
                output.AddRange(draggedBuffer);
            }
            output.Add(remaining[i]);
        }
        if (insertAt >= remaining.Count)
        {
            output.AddRange(draggedBuffer);
        }
        return string.Join("\n", output);
    }

    private static List<HeadingRef> Flatten(IEnumerable<OutlineNode> nodes)
    {
        var list = new List<HeadingRef>();
        void Walk(IEnumerable<OutlineNode> ns)
        {
            foreach (var n in ns)
            {
                list.Add(new HeadingRef(n.SourceLine, n.Level));
                Walk(n.Children);
            }
        }
        Walk(nodes);
        list.Sort((a, b) => a.Line.CompareTo(b.Line));
        return list;
    }

    private static (int Start, int End) ResolveRange(List<HeadingRef> headings, int index, int totalLines)
    {
        var current = headings[index];
        // Markdig's HeadingBlock.Line is 0-indexed, so it directly matches
        // the index produced by source.Split('\n').
        var start = current.Line;
        // Body ends right before the next heading at the SAME or SHALLOWER
        // level. That's the section's natural boundary in Markdown.
        var end = totalLines - 1;
        for (var i = index + 1; i < headings.Count; i++)
        {
            if (headings[i].Level <= current.Level)
            {
                end = headings[i].Line - 1;
                break;
            }
        }
        if (end < start)
        {
            end = start;
        }
        return (start, end);
    }

    private static int ResolveInsertIndex(
        List<string> remaining,
        List<HeadingRef> headings,
        int targetIndex,
        DropPosition position,
        int draggedSrcStart
    )
    {
        if (targetIndex < 0)
        {
            return remaining.Count;
        }
        var target = headings[targetIndex];
        // Adjust target line index for the removal that already happened.
        var adjustedTargetLine = target.Line;
        if (target.Line > draggedSrcStart)
        {
            // dragged section sat before target, so target shifted up by the
            // span. Find target heading inside `remaining` by scanning for the
            // first line whose ATX hashes match the original level.
            adjustedTargetLine = FindHeadingByMatch(remaining, target);
            if (adjustedTargetLine < 0)
            {
                return remaining.Count;
            }
        }
        return position switch
        {
            DropPosition.Before => adjustedTargetLine,
            DropPosition.Inside => SkipToEndOfHeadingLine(remaining, adjustedTargetLine),
            _ => ResolveAfterIndex(remaining, headings, targetIndex, adjustedTargetLine),
        };
    }

    private static int FindHeadingByMatch(List<string> remaining, HeadingRef target)
    {
        var prefix = new string('#', target.Level) + " ";
        for (var i = 0; i < remaining.Count; i++)
        {
            if (remaining[i].StartsWith(prefix, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    private static int SkipToEndOfHeadingLine(List<string> remaining, int headingIndex)
    {
        // "Inside" means: drop becomes the first child of target. Insert at
        // the line right after the heading line itself.
        return Math.Min(headingIndex + 1, remaining.Count);
    }

    private static int ResolveAfterIndex(
        List<string> remaining,
        List<HeadingRef> headings,
        int targetIndex,
        int adjustedTargetLine
    )
    {
        // Determine where the target's body ends so we can insert AFTER it.
        var target = headings[targetIndex];
        var endLine = remaining.Count;
        for (var i = targetIndex + 1; i < headings.Count; i++)
        {
            if (headings[i].Level <= target.Level)
            {
                var prefix = new string('#', headings[i].Level) + " ";
                for (var j = adjustedTargetLine + 1; j < remaining.Count; j++)
                {
                    if (remaining[j].StartsWith(prefix, StringComparison.Ordinal))
                    {
                        endLine = j;
                        break;
                    }
                }
                break;
            }
        }
        return endLine;
    }

    private sealed class HeadingRef
    {
        public HeadingRef(int line, int level)
        {
            Line = line;
            Level = level;
        }

        public int Line { get; }

        public int Level { get; }
    }
}

internal enum DropPosition
{
    Before,
    After,
    Inside,
}
