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
        // Only show the hand cursor when the link would actually do something on
        // click (launchable URL or in-document anchor). A non-actionable link
        // such as a relative file path keeps the default cursor, so the
        // affordance matches reality.
        Cursor = LinkAt(e.GetPosition(this)) is { IsActionable: true } ? HandCursor : Cursor.Default;
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
        // Ignore clicks on non-actionable links (e.g. a relative file path that
        // is neither launchable nor an anchor) so the event is not swallowed
        // without any navigation or feedback.
        if (LinkAt(e.GetPosition(this)) is not { IsActionable: true } link)
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
        public LinkRange(int start, int length, string target, bool isAnchor, bool isActionable)
        {
            Start = start;
            Length = length;
            Target = target;
            IsAnchor = isAnchor;
            IsActionable = isActionable;
        }

        public int Start { get; }

        public int Length { get; }

        public string Target { get; }

        public bool IsAnchor { get; }

        // True when a click will be acted upon (an in-document anchor or a
        // launchable URL). False for links that would otherwise show a hand
        // cursor but do nothing, such as relative or file targets.
        public bool IsActionable { get; }
    }
}
