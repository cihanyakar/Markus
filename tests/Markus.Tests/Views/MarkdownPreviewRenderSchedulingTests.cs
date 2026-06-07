using Markus.Views;

namespace Markus.Tests.Views;

// The preview debounces renders to coalesce rapid keystrokes, but the very
// first paint (an empty buffer receiving its initial document) must skip that
// wait so opening a file feels instant rather than lagging by the debounce
// interval. A hidden view-mode copy must not render at all until it is shown.
public sealed class MarkdownPreviewRenderSchedulingTests
{
    [Theory]
    [InlineData("", "content")] // first real content into an empty buffer
    [InlineData(null, "content")] // unset buffer receiving content
    public void FirstContentRender_IsImmediate(string? lastRendered, string pending)
    {
        MarkdownPreviewControl.ShouldRenderImmediately(lastRendered, pending).ShouldBeTrue();
    }

    [Theory]
    [InlineData("old", "new")] // an edit to already-shown content is debounced
    [InlineData("content", "")] // clearing the buffer is not latency sensitive
    [InlineData("", "")] // nothing meaningful to paint
    [InlineData(null, "")] // unset buffer, still nothing to paint
    public void SubsequentOrEmptyRender_IsDebounced(string? lastRendered, string pending)
    {
        MarkdownPreviewControl.ShouldRenderImmediately(lastRendered, pending).ShouldBeFalse();
    }

    [Theory]
    [InlineData("", "content")] // initial load while hidden
    [InlineData("old", "new")] // edit while hidden
    [InlineData("content", "")] // clear while hidden
    public void HiddenPanel_DefersRender(string? lastRendered, string pending)
    {
        MarkdownPreviewControl
            .DecidePreviewRender(isEffectivelyVisible: false, lastRendered, pending)
            .ShouldBe(PreviewRenderSchedule.Defer);
    }

    [Fact]
    public void VisiblePanel_FirstContent_RendersImmediately()
    {
        MarkdownPreviewControl
            .DecidePreviewRender(isEffectivelyVisible: true, lastRendered: string.Empty, pending: "content")
            .ShouldBe(PreviewRenderSchedule.Immediate);
    }

    [Fact]
    public void VisiblePanel_SubsequentEdit_Debounces()
    {
        MarkdownPreviewControl
            .DecidePreviewRender(isEffectivelyVisible: true, lastRendered: "old", pending: "new")
            .ShouldBe(PreviewRenderSchedule.Debounced);
    }
}
