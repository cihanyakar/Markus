using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Markus.Services;

/// <summary>
/// Walks the controls rendered by <c>MarkdownPreviewControl</c>, splits Runs
/// containing search matches, and exposes navigation helpers for next/prev.
/// Highlight colors are translucent so they layer over any markdown theme.
/// </summary>
internal sealed class PreviewSearcher
{
    private static readonly IBrush MatchBrush = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xE5, 0x6B));
    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0x9F, 0x33));

    private readonly List<Run> _matches = new List<Run>();
    private StringComparison _comparison = StringComparison.OrdinalIgnoreCase;

    public int MatchCount => _matches.Count;

    public int ActiveIndex { get; private set; } = -1;

    public Run? ActiveMatch => ActiveIndex >= 0 && ActiveIndex < _matches.Count ? _matches[ActiveIndex] : null;

    public static Control? FindControlParent(StyledElement? element)
    {
        while (element is not null)
        {
            if (element is Control control)
            {
                return control;
            }
            element = element.Parent;
        }
        return null;
    }

    public void Reset()
    {
        _matches.Clear();
        ActiveIndex = -1;
    }

    public void Highlight(IEnumerable<Control> blocks, string term, bool caseSensitive)
    {
        Reset();
        if (string.IsNullOrEmpty(term))
        {
            return;
        }
        _comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        foreach (var block in blocks)
        {
            if (block is TextBlock tb && tb.Inlines is { } inlines)
            {
                HighlightInlines(inlines, term);
            }
        }
        if (_matches.Count > 0)
        {
            ActiveIndex = 0;
            ApplyActive();
        }
    }

    public bool MoveNext()
    {
        if (_matches.Count == 0)
        {
            return false;
        }
        _matches[ActiveIndex].Background = MatchBrush;
        ActiveIndex = (ActiveIndex + 1) % _matches.Count;
        ApplyActive();
        return true;
    }

    public bool MovePrev()
    {
        if (_matches.Count == 0)
        {
            return false;
        }
        _matches[ActiveIndex].Background = MatchBrush;
        ActiveIndex = (ActiveIndex - 1 + _matches.Count) % _matches.Count;
        ApplyActive();
        return true;
    }

    private static Run CloneRun(Run source, string text)
    {
        return new Run(text)
        {
            FontFamily = source.FontFamily,
            FontSize = source.FontSize,
            FontWeight = source.FontWeight,
            FontStyle = source.FontStyle,
            Foreground = source.Foreground,
            TextDecorations = source.TextDecorations,
        };
    }

    private void ApplyActive()
    {
        if (ActiveIndex >= 0 && ActiveIndex < _matches.Count)
        {
            _matches[ActiveIndex].Background = ActiveBrush;
        }
    }

    private void HighlightInlines(InlineCollection inlines, string term)
    {
        var snapshot = inlines.ToList();
        var i = 0;
        foreach (var inline in snapshot)
        {
            var step = ProcessInline(inlines, inline, term, i);
            i += step;
        }
    }

    private int ProcessInline(InlineCollection inlines, Inline inline, string term, int index)
    {
        if (inline is Span span)
        {
            HighlightInlines(span.Inlines, term);
            return 1;
        }
        if (inline is not Run run || ReferenceEquals(run.Background, MatchBrush))
        {
            return 1;
        }
        var split = SplitRun(run, term);
        if (split.Count <= 1)
        {
            return 1;
        }
        inlines.RemoveAt(index);
        for (var j = 0; j < split.Count; j++)
        {
            inlines.Insert(index + j, split[j]);
        }
        return split.Count;
    }

    private List<Inline> SplitRun(Run source, string term)
    {
        var text = source.Text ?? string.Empty;
        if (text.Length == 0)
        {
            return new List<Inline> { source };
        }
        var parts = new List<Inline>();
        var cursor = 0;
        while (cursor < text.Length)
        {
            var idx = text.IndexOf(term, cursor, _comparison);
            if (idx < 0)
            {
                if (cursor < text.Length)
                {
                    parts.Add(CloneRun(source, text[cursor..]));
                }
                break;
            }
            if (idx > cursor)
            {
                parts.Add(CloneRun(source, text[cursor..idx]));
            }
            var matchRun = CloneRun(source, text.Substring(idx, term.Length));
            matchRun.Background = MatchBrush;
            _matches.Add(matchRun);
            parts.Add(matchRun);
            cursor = idx + term.Length;
        }
        return parts.Count > 0 ? parts : new List<Inline> { source };
    }
}
