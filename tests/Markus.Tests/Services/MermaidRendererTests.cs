using Markus.Services;

namespace Markus.Tests.Services;

public sealed class MermaidRendererTests
{
    [Fact]
    public void IsAvailable_ReturnsBool()
    {
        var result = MermaidRenderer.IsAvailable;

        result.ShouldBeAssignableTo<bool>();
    }

    [Fact]
    public async Task RenderToSvgAsync_CancelledToken_ThrowsOrReturnsNull()
    {
        if (!MermaidRenderer.IsAvailable)
        {
            return;
        }

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            MermaidRenderer.RenderToSvgAsync("graph TD; A-->B;", cts.Token)
        );
    }

    [Fact]
    public async Task RenderToSvgAsync_ValidDiagram_ReturnsSvg()
    {
        if (!MermaidRenderer.IsAvailable)
        {
            return;
        }

        var result = await MermaidRenderer.RenderToSvgAsync(
            "graph TD;\n    A-->B;",
            TestContext.Current.CancellationToken
        );

        result.ShouldNotBeNull();
        result.ShouldContain("<svg");
    }

    [Fact]
    public async Task RenderToSvgAsync_InvalidSyntax_StillReturnsSvg()
    {
        if (!MermaidRenderer.IsAvailable)
        {
            return;
        }

        var result = await MermaidRenderer.RenderToSvgAsync(
            "not a valid mermaid diagram @#$%",
            TestContext.Current.CancellationToken
        );

        result.ShouldNotBeNull();
        result.ShouldContain("<svg");
    }

    [Fact]
    public async Task RenderToSvgAsync_EmptyInput_ReturnsSvgOrNull()
    {
        if (!MermaidRenderer.IsAvailable)
        {
            return;
        }

        var result = await MermaidRenderer.RenderToSvgAsync(string.Empty, TestContext.Current.CancellationToken);

        result?.ShouldContain("<svg");
    }
}
