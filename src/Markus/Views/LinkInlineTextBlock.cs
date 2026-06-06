using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Markus.Views;

// A SelectableTextBlock that renders links as ordinary inline runs (so they sit
// on the text baseline) and resolves clicks by hit-testing the text layout,
// rather than embedding clickable child controls that float above the line.
internal sealed class LinkInlineTextBlock : SelectableTextBlock
{
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    private readonly List<LinkRange> _links = new();

    public Action<string>? AnchorActivated { get; set; }

    public Action<string>? UrlActivated { get; set; }

    public void SetLinks(List<LinkRange> links)
    {
        _links.Clear();
        _links.AddRange(links);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_links.Count == 0)
        {
            return;
        }
        Cursor = LinkAt(e.GetPosition(this)) is null ? Cursor.Default : HandCursor;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_links.Count == 0 || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }
        // A drag selection leaves a non-empty range; only a plain click opens.
        if (SelectionStart != SelectionEnd)
        {
            return;
        }
        if (LinkAt(e.GetPosition(this)) is not { } link)
        {
            return;
        }
        if (link.IsAnchor)
        {
            AnchorActivated?.Invoke(link.Target);
        }
        else
        {
            UrlActivated?.Invoke(link.Target);
        }
        e.Handled = true;
    }

    private LinkRange? LinkAt(Point point)
    {
        var layout = TextLayout;
        if (layout is null)
        {
            return null;
        }
        var hit = layout.HitTestPoint(new Point(point.X - Padding.Left, point.Y - Padding.Top));
        if (!hit.IsInside)
        {
            return null;
        }
        foreach (var link in _links)
        {
            if (hit.TextPosition >= link.Start && hit.TextPosition < link.Start + link.Length)
            {
                return link;
            }
        }
        return null;
    }

    internal readonly struct LinkRange
    {
        public LinkRange(int start, int length, string target, bool isAnchor)
        {
            Start = start;
            Length = length;
            Target = target;
            IsAnchor = isAnchor;
        }

        public int Start { get; }

        public int Length { get; }

        public string Target { get; }

        public bool IsAnchor { get; }
    }
}
