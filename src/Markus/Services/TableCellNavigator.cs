using System.Runtime.InteropServices;

namespace Markus.Services;

/// <summary>
/// Locates the GFM pipe table containing a caret offset and computes
/// cell-to-cell navigation. Pure logic, no AvaloniaEdit references; the
/// editor wraps this with key dispatch and reflow triggers.
/// </summary>
internal static class TableCellNavigator
{
    public static bool TryFindTableAt(string source, int caretOffset, out TableRegion region)
    {
        region = default!;
        if (string.IsNullOrEmpty(source) || caretOffset < 0 || caretOffset > source.Length)
        {
            return false;
        }

        var lines = SplitLines(source);
        var caretLine = LineIndexAt(source, caretOffset);

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

    private static int LineIndexAt(string source, int offset)
    {
        var line = 0;
        for (var i = 0; i < offset && i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                line++;
            }
        }
        return line;
    }

    private static List<LineRange> SplitLines(string source)
    {
        var result = new List<LineRange>();
        var start = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                result.Add(new LineRange(start, source[start..i]));
                start = i + 1;
            }
        }
        if (start <= source.Length)
        {
            result.Add(new LineRange(start, source[start..]));
        }
        return result;
    }

    private static bool LooksLikeTableRow(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '|')
        {
            return false;
        }
        return trimmed.Where(c => c == '|').Take(2).Count() >= 2;
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

/// <summary>One row's worth of cells, plus index metadata.</summary>
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
