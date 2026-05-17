using Markus.Models;

namespace Markus.Tests.Models;

public sealed class AppSettingsTests
{
    [Fact]
    public void Defaults_MatchDocumentedValues()
    {
        var sut = new AppSettings();

        sut.Renderer.ShouldBe(RendererKind.Native);
        sut.Language.ShouldBe("en");
        sut.Theme.ShouldBe("GitHubDark");
        sut.CodeTheme.ShouldBe("Auto");
        sut.DefaultViewMode.ShouldBe(ViewMode.Preview);
        sut.ShowOutline.ShouldBeFalse();
        sut.FontSize.ShouldBe(16.0);
        sut.MonoFont.ShouldBe("Iosevka");
        sut.ThemeMode.ShouldBe("System");
        sut.RecentFiles.ShouldBeEmpty();
    }

    [Fact]
    public void Clone_ReturnsDistinctInstance()
    {
        var source = new AppSettings();

        var clone = source.Clone();

        clone.ShouldNotBeSameAs(source);
    }

    [Fact]
    public void Clone_CopiesAllScalarValues()
    {
        var source = new AppSettings
        {
            Renderer = RendererKind.Placeholder,
            Language = "tr",
            Theme = "SolarizedLight",
            CodeTheme = "Monokai",
            ThemeMode = "Dark",
            DefaultViewMode = ViewMode.Source,
            ShowOutline = true,
            FontSize = 22.5,
            MonoFont = "Cascadia Code",
        };

        var clone = source.Clone();

        clone.Renderer.ShouldBe(RendererKind.Placeholder);
        clone.Language.ShouldBe("tr");
        clone.Theme.ShouldBe("SolarizedLight");
        clone.CodeTheme.ShouldBe("Monokai");
        clone.ThemeMode.ShouldBe("Dark");
        clone.DefaultViewMode.ShouldBe(ViewMode.Source);
        clone.ShowOutline.ShouldBeTrue();
        clone.FontSize.ShouldBe(22.5);
        clone.MonoFont.ShouldBe("Cascadia Code");
    }

    [Fact]
    public void Clone_RecentFilesIsFreshList_MutatingCloneDoesNotAffectSource()
    {
        var source = new AppSettings();
        source.RecentFiles.Add("a.md");
        source.RecentFiles.Add("b.md");

        var clone = source.Clone();
        clone.RecentFiles.ShouldNotBeSameAs(source.RecentFiles);

        clone.RecentFiles.Add("c.md");

        source.RecentFiles.Count.ShouldBe(2);
        string[] expectedSource = ["a.md", "b.md"];
        string[] expectedClone = ["a.md", "b.md", "c.md"];
        source.RecentFiles.ShouldBe(expectedSource);
        clone.RecentFiles.ShouldBe(expectedClone);
    }

    [Fact]
    public void Clone_RecentFilesIsFreshList_MutatingSourceDoesNotAffectClone()
    {
        var source = new AppSettings();
        source.RecentFiles.Add("x.md");

        var clone = source.Clone();

        source.RecentFiles.Add("y.md");

        clone.RecentFiles.Count.ShouldBe(1);
        string[] expectedClone = ["x.md"];
        string[] expectedSource = ["x.md", "y.md"];
        clone.RecentFiles.ShouldBe(expectedClone);
        source.RecentFiles.ShouldBe(expectedSource);
    }

    [Fact]
    public void Clone_RoundTrip_PreservesAllValues()
    {
        var source = new AppSettings
        {
            Renderer = RendererKind.Placeholder,
            Language = "de",
            Theme = "Nord",
            CodeTheme = "Dracula",
            ThemeMode = "Light",
            DefaultViewMode = ViewMode.SplitVertical,
            ShowOutline = true,
            FontSize = 18.0,
            MonoFont = "Fira Code",
        };
        source.RecentFiles.Add("one.md");
        source.RecentFiles.Add("two.md");
        source.RecentFiles.Add("three.md");

        var clone = source.Clone();

        clone.Renderer.ShouldBe(source.Renderer);
        clone.Language.ShouldBe(source.Language);
        clone.Theme.ShouldBe(source.Theme);
        clone.CodeTheme.ShouldBe(source.CodeTheme);
        clone.ThemeMode.ShouldBe(source.ThemeMode);
        clone.DefaultViewMode.ShouldBe(source.DefaultViewMode);
        clone.ShowOutline.ShouldBe(source.ShowOutline);
        clone.FontSize.ShouldBe(source.FontSize);
        clone.MonoFont.ShouldBe(source.MonoFont);
        clone.RecentFiles.ShouldBe(source.RecentFiles);
    }
}
