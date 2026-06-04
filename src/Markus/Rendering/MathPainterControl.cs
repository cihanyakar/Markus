using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CSharpMath.Avalonia;

namespace Markus.Rendering;

internal sealed class MathPainterControl : Control
{
    private readonly MathPainter _painter;

    public MathPainterControl(MathPainter painter, float width, float height)
    {
        _painter = painter;
        Width = width;
        Height = height;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var canvas = new AvaloniaCanvas(context, new Size(Width, Height));
        _painter.Draw(canvas, CSharpMath.Rendering.FrontEnd.TextAlignment.Center);
    }
}
