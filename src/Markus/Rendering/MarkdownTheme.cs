using Avalonia.Media;

namespace Markus.Rendering;

internal sealed class MarkdownTheme
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool IsDark { get; init; }

    public Color Background { get; init; }

    public Color Foreground { get; init; }

    public Color Accent { get; init; }

    public Color CodeBackground { get; init; }

    public Color CodeForeground { get; init; }

    public Color CodeBorder { get; init; }

    public Color QuoteAccent { get; init; }

    public Color Muted { get; init; }
}
