using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Markus.Services;

/// <summary>
/// Locates the GFM pipe table containing a caret offset and computes
/// cell-to-cell navigation. Pure logic, no AvaloniaEdit references; the
/// editor wraps this with key dispatch and reflow triggers.
/// </summary>
internal static class TableCellNavigator
{
    public static bool TryFindTableAt(string source, int caretOffset, [NotNullWhen(true)] out TableRegion? region)
    {
        region = null;
        if (string.IsNullOrEmpty(source) || caretOffset < 0 || caretOffset > source.Length)
        {
            return false;
        }

        var (lines, caretLine) = SplitLinesAndLocate(source, caretOffset);

        // Walk backward to find a candidate header row (first table row above caret).
        var headerLine = -1;
        for (var i = caretLine; i >= 0; i--)
        {
            if (!LooksLikeTableRow(lines[i].Text))
            {
                break;
            }
            headerLine = i;
        }
        if (headerLine < 0 || headerLine + 1 >= lines.Count)
        {
            return false;
        }
        if (!IsDelimiterRow(lines[headerLine + 1].Text))
        {
            return false;
        }

        // Walk forward to find the last contiguous table row.
        var endLine = headerLine + 1;
        for (var i = endLine + 1; i < lines.Count; i++)
        {
            if (!LooksLikeTableRow(lines[i].Text))
            {
                break;
            }
            endLine = i;
        }

        var rows = new List<IReadOnlyList<CellRange>>(endLine - headerLine + 1);
        for (var i = headerLine; i <= endLine; i++)
        {
            rows.Add(SplitRowCells(lines[i]));
        }

        region = new TableRegion(
            startLine: headerLine,
            endLine: endLine,
            headerLine: headerLine,
            delimiterLine: headerLine + 1,
            rows: rows
        );
        return true;
    }

    public static CellRange? NextCell(TableRegion region, int currentOffset, bool forward)
    {
        var (rowIndex, cellIndex) = LocateCell(region, currentOffset);
        if (rowIndex < 0)
        {
            return null;
        }
        return forward ? StepForward(region, rowIndex, cellIndex) : StepBackward(region, rowIndex, cellIndex);
    }

    public static InsertRowResult InsertEmptyRow(string source, TableRegion region)
    {
        var columnCount = region.Rows[0].Count;
        var emptyRow = BuildEmptyRow(columnCount);

        var lastDataRow = region.Rows[^1];
        // Find the end-of-line offset of the table's last row.
        var insertionOffset = FindLineEndOffset(source, lastDataRow[^1]);
        var insertion = "\n" + emptyRow;
        var newSource = source.Insert(insertionOffset, insertion);

        // Caret lands at the first content column of the new row.
        //   "|   |"  -> position after "| " == insertionOffset + 1 (for \n) + 2 (for "| ").
        var newCaret = insertionOffset + 1 + 2;
        return new InsertRowResult(newSource, newCaret);
    }

    private static (int Row, int Cell) LocateCell(TableRegion region, int offset)
    {
        for (var r = 0; r < region.Rows.Count; r++)
        {
            var row = region.Rows[r];
            for (var c = 0; c < row.Count; c++)
            {
                var cell = row[c];
                if (offset >= cell.Offset && offset <= cell.Offset + cell.Length)
                {
                    return (r, c);
                }
            }
        }
        return (-1, -1);
    }

    private static CellRange? StepForward(TableRegion region, int row, int cell)
    {
        if (cell + 1 < region.Rows[row].Count)
        {
            return region.Rows[row][cell + 1];
        }
        var nextRow = row + 1;
        // Row index 1 in Rows corresponds to the delimiter line; skip it. For a
        // header+delimiter-only table (Rows.Count == 2) this advances past the
        // table, and the bounds check below returns null.
        if (nextRow == 1)
        {
            nextRow = 2;
        }
        if (nextRow >= region.Rows.Count)
        {
            return null;
        }
        return region.Rows[nextRow].Count > 0 ? region.Rows[nextRow][0] : null;
    }

    private static CellRange? StepBackward(TableRegion region, int row, int cell)
    {
        if (cell > 0)
        {
            return region.Rows[row][cell - 1];
        }
        var prevRow = row - 1;
        // Skip the delimiter row when stepping backward.
        if (prevRow == 1)
        {
            prevRow = 0;
        }
        if (prevRow < 0)
        {
            return null;
        }
        var prev = region.Rows[prevRow];
        return prev.Count > 0 ? prev[^1] : null;
    }

    private static string BuildEmptyRow(int columnCount)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('|');
        for (var c = 0; c < columnCount; c++)
        {
            sb.Append("   |");
        }
        return sb.ToString();
    }

    private static int FindLineEndOffset(string source, CellRange anyCellInLine)
    {
        var i = anyCellInLine.Offset;
        while (i < source.Length && source[i] != '\n')
        {
            i++;
        }
        return i;
    }

    private static (List<LineRange> Lines, int CaretLineIndex) SplitLinesAndLocate(string source, int caretOffset)
    {
        var lines = new List<LineRange>();
        var start = 0;
        var caretLine = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (i == caretOffset)
            {
                caretLine = lines.Count;
            }
            if (source[i] == '\n')
            {
                lines.Add(new LineRange(start, source[start..i]));
                start = i + 1;
            }
        }
        if (caretOffset >= source.Length)
        {
            caretLine = lines.Count;
        }
        if (start <= source.Length)
        {
            lines.Add(new LineRange(start, source[start..]));
        }
        return (lines, caretLine);
    }

    private static bool LooksLikeTableRow(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '|')
        {
            return false;
        }
        var pipes = 0;
        for (var i = 0; i < trimmed.Length; i++)
        {
            if (trimmed[i] == '|')
            {
                pipes++;
                if (pipes >= 2)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsDelimiterRow(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '|')
        {
            return false;
        }
        var hasDash = false;
        foreach (var c in trimmed)
        {
            if (c is not '|' and not '-' and not ':' and not ' ')
            {
                return false;
            }
            if (c == '-')
            {
                hasDash = true;
            }
        }
        return hasDash;
    }

    private static List<CellRange> SplitRowCells(LineRange line)
    {
        var cells = new List<CellRange>();
        var span = line.Text;
        var leading = 0;
        while (leading < span.Length && span[leading] == ' ')
        {
            leading++;
        }
        // Skip the row's opening pipe.
        var i = leading;
        if (i < span.Length && span[i] == '|')
        {
            i++;
        }
        var cellStart = i;
        for (; i < span.Length; i++)
        {
            if (span[i] == '|' && !IsEscaped(span, i))
            {
                cells.Add(new CellRange(line.Offset + cellStart, i - cellStart));
                cellStart = i + 1;
            }
        }
        // No trailing cell after the last unescaped pipe (closing pipe).
        return cells;
    }

    private static bool IsEscaped(string text, int index)
    {
        var backslashes = 0;
        for (var j = index - 1; j >= 0 && text[j] == '\\'; j--)
        {
            backslashes++;
        }
        return (backslashes & 1) == 1;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct LineRange(int offset, string text)
    {
        public int Offset => offset;

        public string Text => text;
    }
}

/// <summary>
/// A located GFM pipe table: line bounds, header/delimiter indices, and
/// cell ranges per row (including the delimiter row at index 1 of <see cref="Rows"/>).
/// </summary>
internal sealed record TableRegion(
    int startLine,
    int endLine,
    int headerLine,
    int delimiterLine,
    IReadOnlyList<IReadOnlyList<CellRange>> rows
)
{
    public int StartLine => startLine;

    public int EndLine => endLine;

    public int HeaderLine => headerLine;

    public int DelimiterLine => delimiterLine;

    public IReadOnlyList<IReadOnlyList<CellRange>> Rows => rows;
}

/// <summary>Absolute source offset/length for a single cell's content.</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct CellRange(int offset, int length)
{
    public int Offset => offset;

    public int Length => length;
}

/// <summary>Result of inserting an empty row into a pipe table.</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct InsertRowResult(string newSource, int newCaretOffset)
{
    public string NewSource => newSource;

    public int NewCaretOffset => newCaretOffset;
}
