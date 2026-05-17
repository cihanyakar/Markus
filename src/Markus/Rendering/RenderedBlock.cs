using Avalonia.Controls;

namespace Markus.Rendering;

internal readonly record struct RenderedBlock(Control control, int sourceLine)
{
    public Control Control => control;

    public int SourceLine => sourceLine;
}
