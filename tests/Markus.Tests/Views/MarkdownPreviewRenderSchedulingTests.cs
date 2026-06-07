using Markus.Views;

namespace Markus.Tests.Views;

// The preview debounces renders to coalesce rapid keystrokes, but the very
// first paint (an empty buffer receiving its initial document) must skip that
// wait so opening a file feels instant rather than lagging by the debounce
// interval.
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
}
