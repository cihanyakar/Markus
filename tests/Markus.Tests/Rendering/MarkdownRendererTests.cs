using System.Reflection;
using Markus.Rendering;

namespace Markus.Tests.Rendering;

public sealed class MarkdownRendererTests : IDisposable
{
    private static readonly MethodInfo FsMethod = ResolveFsMethod();

    private readonly double _originalBaseFontSize = MarkdownRenderer.BaseFontSize;
    private readonly double _originalMermaidScale = MarkdownRenderer.MermaidScale;
    private readonly bool _originalWrapCode = MarkdownRenderer.WrapCode;

    /// <inheritdoc />
    public void Dispose()
    {
        MarkdownRenderer.BaseFontSize = _originalBaseFontSize;
        MarkdownRenderer.MermaidScale = _originalMermaidScale;
        MarkdownRenderer.WrapCode = _originalWrapCode;
    }

    [Fact]
    public void BaseFontSize_Default_Is16()
    {
        MarkdownRenderer.BaseFontSize.ShouldBe(16.0);
    }

    [Fact]
    public void MermaidScale_Default_Is1()
    {
        MarkdownRenderer.MermaidScale.ShouldBe(1.0);
    }

    [Fact]
    public void WrapCode_Default_IsTrue()
    {
        MarkdownRenderer.WrapCode.ShouldBeTrue();
    }

    [Fact]
    public void Fs_AtDefaultBase_ReturnsSameSize()
    {
        MarkdownRenderer.BaseFontSize = 16.0;

        InvokeFs(16).ShouldBe(16.0);
    }

    [Fact]
    public void Fs_AtDoubleBase_ReturnsDoubleSize()
    {
        MarkdownRenderer.BaseFontSize = 32.0;

        InvokeFs(16).ShouldBe(32.0);
    }

    [Fact]
    public void Fs_HalfSizeAtDefaultBase_ReturnsHalfSize()
    {
        MarkdownRenderer.BaseFontSize = 16.0;

        InvokeFs(8).ShouldBe(8.0);
    }

    [Fact]
    public void Fs_AtHalfBase_ReturnsHalfSize()
    {
        MarkdownRenderer.BaseFontSize = 8.0;

        InvokeFs(16).ShouldBe(8.0);
    }

    [Fact]
    public void Fs_ZeroSize_ReturnsZero()
    {
        MarkdownRenderer.BaseFontSize = 16.0;

        InvokeFs(0).ShouldBe(0.0);
    }

    [Fact]
    public void Fs_ZeroBase_ReturnsZero()
    {
        MarkdownRenderer.BaseFontSize = 0.0;

        InvokeFs(16).ShouldBe(0.0);
    }

    [Fact]
    public void Fs_NegativeSize_ReturnsNegative()
    {
        MarkdownRenderer.BaseFontSize = 16.0;

        InvokeFs(-10).ShouldBe(-10.0);
    }

    [Fact]
    public void Fs_VeryLargeSize_ScalesCorrectly()
    {
        MarkdownRenderer.BaseFontSize = 32.0;

        InvokeFs(1000).ShouldBe(2000.0);
    }

    [Fact]
    public void Fs_FractionalBaseSize_ScalesCorrectly()
    {
        MarkdownRenderer.BaseFontSize = 14.5;

        var result = InvokeFs(16);
        result.ShouldBe(16.0 * 14.5 / 16.0, 0.0001);
    }

    [Fact]
    public void Fs_SmallFractionalSize_PreservesPrecision()
    {
        MarkdownRenderer.BaseFontSize = 20.0;

        InvokeFs(0.5).ShouldBe(0.5 * 20.0 / 16.0, 0.0001);
    }

    [Fact]
    public void Fs_ScalesProportionallyToBaseFontSize()
    {
        // Verify the relationship: doubling BaseFontSize doubles the output
        MarkdownRenderer.BaseFontSize = 12.0;
        var atTwelve = InvokeFs(15);

        MarkdownRenderer.BaseFontSize = 24.0;
        var atTwentyFour = InvokeFs(15);

        atTwentyFour.ShouldBe(atTwelve * 2.0, 0.0001);
    }

    [Fact]
    public void Fs_ChangingBaseFontSize_AffectsSubsequentCalls()
    {
        // This proves Fs reads BaseFontSize on every call, not caching the initial value
        MarkdownRenderer.BaseFontSize = 16.0;
        var first = InvokeFs(10);

        MarkdownRenderer.BaseFontSize = 48.0;
        var second = InvokeFs(10);

        first.ShouldBe(10.0);
        second.ShouldBe(30.0);
        second.ShouldNotBe(first);
    }

    [Fact]
    public void BaseFontSize_CanBeSet_RetainsValue()
    {
        MarkdownRenderer.BaseFontSize = 24.0;

        MarkdownRenderer.BaseFontSize.ShouldBe(24.0);
    }

    [Fact]
    public void MermaidScale_CanBeSet_RetainsValue()
    {
        MarkdownRenderer.MermaidScale = 2.5;

        MarkdownRenderer.MermaidScale.ShouldBe(2.5);
    }

    [Fact]
    public void WrapCode_SetToFalse_RetainsValue()
    {
        MarkdownRenderer.WrapCode = false;

        MarkdownRenderer.WrapCode.ShouldBeFalse();
    }

    private static MethodInfo ResolveFsMethod()
    {
        var method = typeof(MarkdownRenderer).GetMethod(
            "Fs",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            [typeof(double)],
            null
        );
        method.ShouldNotBeNull("Expected a private static method Fs(double) on MarkdownRenderer");
        return method;
    }

    private static double InvokeFs(double size)
    {
        return (double)FsMethod.Invoke(null, [size])!;
    }
}
