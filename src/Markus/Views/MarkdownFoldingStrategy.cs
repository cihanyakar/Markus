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

        for (var i = 1; i <= document.LineCount; i++)
        {
            var line = document.GetLineByNumber(i);
            var text = document.GetText(line);
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
        return foldings.OrderBy(f => f.StartOffset);
    }

    private static void AppendFold(List<NewFolding> list, int start, int end)
    {
        if (end <= start)
        {
            return;
        }
        list.Add(new NewFolding(start, end));
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
