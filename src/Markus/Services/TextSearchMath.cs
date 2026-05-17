namespace Markus.Services;

/// <summary>
/// Pure-string search helpers used by the source-editor find/replace flow.
/// Kept UI-free so the math is testable without an AvaloniaEdit document.
/// </summary>
internal static class TextSearchMath
{
    public static int CountMatches(string text, string term, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(term))
        {
            return 0;
        }
        var comparison = ComparisonFor(caseSensitive);
        var count = 0;
        var idx = 0;
        while ((idx = text.IndexOf(term, idx, comparison)) >= 0)
        {
            count++;
            idx += term.Length;
        }
        return count;
    }

    public static int CurrentMatchIndex(string text, string term, int anchor, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(term))
        {
            return -1;
        }
        var comparison = ComparisonFor(caseSensitive);
        var idx = 0;
        var matchIdx = 0;
        var lastFound = -1;
        while ((idx = text.IndexOf(term, idx, comparison)) >= 0)
        {
            if (idx == anchor)
            {
                return matchIdx;
            }
            if (idx > anchor && lastFound >= 0)
            {
                return lastFound;
            }
            lastFound = matchIdx;
            matchIdx++;
            idx += term.Length;
        }
        return lastFound;
    }

    private static StringComparison ComparisonFor(bool caseSensitive)
    {
        return caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    }
}
