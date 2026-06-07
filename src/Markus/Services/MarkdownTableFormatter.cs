using System.Globalization;
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

    // Column width counts display columns, not UTF-16 units: CJK/emoji are two
    // columns wide (UAX #11). Internal for conformance tests.
    internal static int DisplayWidth(string text)
    {
        var width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsZeroWidth(rune))
            {
                continue;
            }
            width += IsWide(rune.Value) ? 2 : 1;
        }
        return width;
    }

    internal static List<string> SplitCells(string line)
    {
        var span = line.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '|')
        {
            span = span[1..];
        }
        if (span.Length > 0 && span[^1] == '|')
        {
            span = span[..^1];
        }

        var cells = new List<string>();
        var start = 0;
        for (var i = 0; i < span.Length; i++)
        {
            // Split on an unescaped pipe; an escaped \| stays inside the cell.
            if (span[i] == '|' && !IsEscaped(span, i))
            {
                cells.Add(span[start..i].Trim().ToString());
                start = i + 1;
            }
        }
        cells.Add(span[start..].Trim().ToString());
        return cells;
    }

    // Combining marks (Mn/Me) and format characters (Cf, such as the zero-width
    // joiner and zero-width space) take no display column, so they must not count
    // toward a cell's width or decomposed accents misalign the column padding.
    private static bool IsZeroWidth(Rune rune)
    {
        return Rune.GetUnicodeCategory(rune) switch
        {
            UnicodeCategory.NonSpacingMark => true,
            UnicodeCategory.EnclosingMark => true,
            UnicodeCategory.Format => true,
            _ => false,
        };
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
                widest = Math.Max(widest, DisplayWidth(rows[r][c]));
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

    // A pipe is escaped only when preceded by an ODD number of backslashes. An
    // even run means those backslashes escape each other (a literal backslash),
    // leaving the pipe as a real column separator, per GFM escaping rules.
    private static bool IsEscaped(ReadOnlySpan<char> span, int index)
    {
        var backslashes = 0;
        for (var j = index - 1; j >= 0 && span[j] == '\\'; j--)
        {
            backslashes++;
        }
        return (backslashes & 1) == 1;
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
        // Pad by display width, not UTF-16 length, so CJK/emoji cells (which
        // render two columns wide) still line up in a monospace view.
        var pad = width - DisplayWidth(text);
        if (pad <= 0)
        {
            return text;
        }
        return alignment switch
        {
            CellAlignment.Right => new string(' ', pad) + text,
            CellAlignment.Center => new string(' ', pad / 2) + text + new string(' ', pad - (pad / 2)),
            _ => text + new string(' ', pad),
        };
    }

    private static bool IsWide(int v)
    {
        // `and` binds tighter than `or`, so each range stands on its own. Wide =
        // East Asian Wide/Fullwidth plus emoji (rendered two columns wide).
        return v
            is >= 0x1100
                and <= 0x115F // Hangul Jamo
                or >= 0x2E80
                and <= 0x303E // CJK radicals, Kangxi
                or >= 0x3041
                and <= 0x33FF // Kana .. CJK compatibility
                or >= 0x3400
                and <= 0x4DBF // CJK Extension A
                or >= 0x4E00
                and <= 0x9FFF // CJK Unified Ideographs
                or >= 0xA000
                and <= 0xA4CF // Yi
                or >= 0xAC00
                and <= 0xD7A3 // Hangul syllables
                or >= 0xF900
                and <= 0xFAFF // CJK compatibility ideographs
                or >= 0xFE30
                and <= 0xFE4F // CJK compatibility forms
                or >= 0xFF00
                and <= 0xFF60 // Fullwidth forms
                or >= 0xFFE0
                and <= 0xFFE6 // Fullwidth signs
                or >= 0x1F300
                and <= 0x1FAFF // Emoji
                or >= 0x20000
                and <= 0x3FFFD; // CJK Extension B and beyond
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
