using Markus.Rendering;

namespace Markus.Tests.Rendering;

// A markdown document is untrusted, so clicking a link must only ever hand web
// or mail URLs to the OS shell. file:// and custom schemes could launch local
// apps or executables and must be refused.
public sealed class MarkdownRendererSecurityTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/path?q=1")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("HTTPS://Example.com")] // scheme is case-insensitive
    public void IsLaunchableUrl_AllowsWebAndMail(string url)
    {
        MarkdownRenderer.IsLaunchableUrl(url).ShouldBeTrue();
    }

    [Theory]
    [InlineData("file:///Applications/Calculator.app")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vscode://file/etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/usr/bin/open")] // not absolute URI
    [InlineData("#in-document-anchor")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsLaunchableUrl_RejectsEverythingElse(string url)
    {
        MarkdownRenderer.IsLaunchableUrl(url).ShouldBeFalse();
    }

    [Fact]
    public void IsLaunchableUrl_RejectsNull()
    {
        MarkdownRenderer.IsLaunchableUrl(null).ShouldBeFalse();
    }
}
