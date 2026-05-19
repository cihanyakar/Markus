using System.Text;

namespace Markus.Services;

/// <summary>
/// Reflows GitHub-flavored Markdown pipe tables so each column lines up to
/// the widest cell. Honors the alignment delimiter row (<c>:--</c>, <c>:-:</c>,
/// <c>--:</c>) and preserves leading whitespace so nested tables don't shift.
/// Non-table content passes through untouched.
/// </summary>
internal static class MarkdownTableFormatter
{
    private enum CellAlignment
    {
        None,
        Left,
        Right,
        Center,
    }

    public static string Format(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var output = new StringBuilder(source.Length);
        var i = 0;
        while (i < lines.Length)
        {
            if (TryParseTable(lines, i, out var consumed, out var formatted))
            {
                output.Append(formatted);
                i += consumed;
                continue;
            }
            output.Append(lines[i]);
            if (i < lines.Length - 1)
            {
                output.Append('\n');
            }
            i++;
        }
        return output.ToString();
    }

    private static bool TryParseTable(string[] lines, int start, out int consumed, out string formatted)
    {
        consumed = 0;
        formatted = string.Empty;
        if (start + 1 >= lines.Length)
        {
            return false;
        }
        if (!LooksLikeTableRow(lines[start]) || !IsDelimiterRow(lines[start + 1]))
        {
            return false;
        }
        var end = start + 2;
        while (end < lines.Length && LooksLikeTableRow(lines[end]))
        {
            end++;
        }
        var rows = new List<List<string>>();
        for (var r = start; r < end; r++)
        {
            rows.Add(SplitCells(lines[r]));
        }
        var alignment = ParseAlignment(rows[1]);
        var columnCount = rows.Max(r => r.Count);
        NormalizeShape(rows, alignment, columnCount);
        var widths = ComputeColumnWidths(rows, columnCount);
        formatted = EmitRows(rows, widths, alignment, end < lines.Length);
        consumed = end - start;
        return true;
    }

    private static void NormalizeShape(List<List<string>> rows, List<CellAlignment> alignment, int columnCount)
    {
        for (var r = 0; r < rows.Count; r++)
        {
            while (rows[r].Count < columnCount)
            {
                rows[r].Add(string.Empty);
            }
        }
        while (alignment.Count < columnCount)
        {
            alignment.Add(CellAlignment.None);
        }
    }

    private static int[] ComputeColumnWidths(List<List<string>> rows, int columnCount)
    {
        var widths = new int[columnCount];
        for (var c = 0; c < columnCount; c++)
        {
            var widest = 0;
            for (var r = 0; r < rows.Count; r++)
            {
                if (r == 1)
                {
                    continue;
                }
                widest = Math.Max(widest, rows[r][c].Length);
            }
            widths[c] = widest;
        }
        return widths;
    }

    private static string EmitRows(
        List<List<string>> rows,
        int[] widths,
        List<CellAlignment> alignment,
        bool trailingNewline
    )
    {
        var sb = new StringBuilder();
        for (var r = 0; r < rows.Count; r++)
        {
            sb.Append('|');
            for (var c = 0; c < widths.Length; c++)
            {
                if (r == 1)
                {
                    sb.Append(BuildDelimiter(widths[c], alignment[c]));
                }
                else
                {
                    sb.Append(' ');
                    sb.Append(Pad(rows[r][c], widths[c], alignment[c]));
                    sb.Append(' ');
                }
                sb.Append('|');
            }
            if (r < rows.Count - 1 || trailingNewline)
            {
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    private static bool LooksLikeTableRow(string line)
    {
        var trimmed = line.TrimStart();
        if (string.IsNullOrEmpty(trimmed) || trimmed[0] != '|')
        {
            return false;
        }
        return trimmed.Count(c => c == '|') >= 2;
    }

    private static bool IsDelimiterRow(string line)
    {
        var cells = SplitCells(line);
        if (cells.Count == 0)
        {
            return false;
        }
        foreach (var cell in cells)
        {
            var t = cell.Trim();
            if (t.Length == 0)
            {
                return false;
            }
            var inner = t;
            if (inner[0] == ':')
            {
                inner = inner[1..];
            }
            if (inner.EndsWith(':'))
            {
                inner = inner[..^1];
            }
            if (inner.Length == 0 || !inner.All(c => c == '-'))
            {
                return false;
            }
        }
        return true;
    }

    private static List<string> SplitCells(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }
        if (trimmed.EndsWith('|'))
        {
            trimmed = trimmed[..^1];
        }
        return trimmed.Split('|').Select(s => s.Trim()).ToList();
    }

    private static List<CellAlignment> ParseAlignment(IEnumerable<string> delimiterCells)
    {
        var result = new List<CellAlignment>();
        foreach (var cell in delimiterCells)
        {
            var t = cell.Trim();
            var left = t.StartsWith(':');
            var right = t.EndsWith(':');
            result.Add(
                (left, right) switch
                {
                    (true, true) => CellAlignment.Center,
                    (false, true) => CellAlignment.Right,
                    (true, false) => CellAlignment.Left,
                    _ => CellAlignment.None,
                }
            );
        }
        return result;
    }

    private static string Pad(string text, int width, CellAlignment alignment)
    {
        if (text.Length >= width)
        {
            return text;
        }
        return alignment switch
        {
            CellAlignment.Right => text.PadLeft(width),
            CellAlignment.Center => PadCenter(text, width),
            _ => text.PadRight(width),
        };
    }

    private static string PadCenter(string text, int width)
    {
        var total = width - text.Length;
        var left = total / 2;
        var right = total - left;
        return new string(' ', left) + text + new string(' ', right);
    }

    private static string BuildDelimiter(int width, CellAlignment alignment)
    {
        // Delimiter cell width = data cell width + 2 (two surrounding spaces
        // in the rendered data row). Colons mark alignment; the rest is '-'.
        var total = width + 2;
        var dashes = total;
        var prefix = string.Empty;
        var suffix = string.Empty;
        switch (alignment)
        {
            case CellAlignment.Center:
                prefix = ":";
                suffix = ":";
                dashes = total - 2;
                break;
            case CellAlignment.Left:
                prefix = ":";
                dashes = total - 1;
                break;
            case CellAlignment.Right:
                suffix = ":";
                dashes = total - 1;
                break;
        }
        return prefix + new string('-', Math.Max(3, dashes)) + suffix;
    }
}
