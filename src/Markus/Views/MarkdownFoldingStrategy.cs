using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace Markus.Views;

/// <summary>
/// Walks the document for ATX headings (<c>#</c>..<c>######</c>) and produces
/// folds that wrap each section's body. Folds are closed when a heading at
/// the same or higher rank starts a new section.
/// </summary>
internal sealed class MarkdownFoldingStrategy
{
    public static IEnumerable<NewFolding> CreateFoldings(TextDocument document)
    {
        var foldings = new List<NewFolding>();
        var stack = new Stack<HeadingFrame>();
        var inFence = false;

        for (var i = 1; i <= document.LineCount; i++)
        {
            var line = document.GetLineByNumber(i);
            // Only heading (`#`) and fence (``` / ~~~) lines can change folds.
            // Peek the first non-whitespace char from the document and skip every
            // other line without allocating a per-line string. This runs on every
            // keystroke, so skipping the GetText copy for prose lines is the win.
            var firstChar = FirstNonWhitespaceChar(document, line);
            if (firstChar is not '#' and not '`' and not '~')
            {
                continue;
            }
            var text = document.GetText(line);
            // A `#` inside a fenced code block is code, not a heading. Toggle on
            // the fence markers so those lines never start a fold.
            if (IsFenceMarker(text))
            {
                inFence = !inFence;
                continue;
            }
            if (inFence)
            {
                continue;
            }
            var level = ParseHeadingLevel(text);
            if (level == 0)
            {
                continue;
            }
            while (stack.Count > 0 && stack.Peek().Level >= level)
            {
                var frame = stack.Pop();
                // End the fold at the previous line's EndOffset (just before
                // its trailing newline). That newline stays outside the fold,
                // so the collapsed placeholder lands on its own visual line
                // between the section and the next heading.
                var foldEnd = line.PreviousLine?.EndOffset ?? line.Offset;
                AppendFold(foldings, frame.TitleEndOffset, foldEnd);
            }
            // Start the fold after the heading line's trailing newline so the
            // heading text stays visible and the placeholder begins on the
            // next visual line.
            var foldStart = line.Offset + line.TotalLength;
            stack.Push(new HeadingFrame { Level = level, TitleEndOffset = foldStart });
        }
        while (stack.Count > 0)
        {
            var frame = stack.Pop();
            var lastLine = document.GetLineByNumber(document.LineCount);
            AppendFold(foldings, frame.TitleEndOffset, lastLine.EndOffset);
        }
        // Sort in place (no LINQ iterator/array). Fold start offsets are distinct
        // (each begins after a different heading), so stability is irrelevant.
        foldings.Sort(static (a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }

    private static char FirstNonWhitespaceChar(TextDocument document, DocumentLine line)
    {
        var end = line.EndOffset;
        for (var offset = line.Offset; offset < end; offset++)
        {
            var c = document.GetCharAt(offset);
            if (!char.IsWhiteSpace(c))
            {
                return c;
            }
        }
        return '\0';
    }

    private static void AppendFold(List<NewFolding> list, int start, int end)
    {
        if (end <= start)
        {
            return;
        }
        list.Add(new NewFolding(start, end));
    }

    private static bool IsFenceMarker(string lineText)
    {
        var trimmed = lineText.AsSpan().TrimStart();
        return trimmed.StartsWith("```", StringComparison.Ordinal)
            || trimmed.StartsWith("~~~", StringComparison.Ordinal);
    }

    private static int ParseHeadingLevel(string lineText)
    {
        var hashes = 0;
        while (hashes < lineText.Length && lineText[hashes] == '#')
        {
            hashes++;
        }
        if (hashes is 0 or > 6)
        {
            return 0;
        }
        if (hashes >= lineText.Length || lineText[hashes] != ' ')
        {
            return 0;
        }
        return hashes;
    }

    private sealed class HeadingFrame
    {
        public int Level { get; init; }

        public int TitleEndOffset { get; init; }
    }
}
