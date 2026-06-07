using Markus.Views;

namespace Markus.Tests.Views;

// TextMate grammar/theme loading is expensive and runs on the UI thread. The
// window keeps one editor per view mode attached at once, so loading it for the
// hidden copies blocks launch for nothing. It must install once, only when the
// editor is the active (effectively visible) view.
public sealed class MarkdownTextEditorTextMateTests
{
    [Fact]
    public void VisibleAndNotYetInstalled_Installs()
    {
        MarkdownTextEditor.ShouldInstallTextMate(alreadyInstalled: false, isEffectivelyVisible: true).ShouldBeTrue();
    }

    [Fact]
    public void Hidden_DoesNotInstall()
    {
        MarkdownTextEditor.ShouldInstallTextMate(alreadyInstalled: false, isEffectivelyVisible: false).ShouldBeFalse();
    }

    [Fact]
    public void AlreadyInstalled_DoesNotReinstall()
    {
        MarkdownTextEditor.ShouldInstallTextMate(alreadyInstalled: true, isEffectivelyVisible: true).ShouldBeFalse();
    }
}
