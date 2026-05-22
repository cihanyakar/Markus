using System.Text.Json;
using Markus.Models;
using Markus.Services;

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
        sut.OutlinePlacement.ShouldBe(OutlinePlacement.Right);
        sut.FontSize.ShouldBe(16.0);
        sut.MonoFont.ShouldBe("JetBrains Mono");
        sut.ThemeMode.ShouldBe("System");
        sut.IsSourceSoftWrap.ShouldBeFalse();
        sut.IsPreviewSoftWrap.ShouldBeTrue();
        sut.MermaidScale.ShouldBe(1.0);
        sut.RecentFiles.ShouldBeEmpty();
        sut.LastOpenedFile.ShouldBeNull();
        sut.LastScrollLine.ShouldBe(0);
        sut.RestoreSessionOnLaunch.ShouldBeFalse();
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
            OutlinePlacement = OutlinePlacement.Left,
            IsSourceSoftWrap = true,
            IsPreviewSoftWrap = false,
            MermaidScale = 2.5,
            LastOpenedFile = "/tmp/test.md",
            LastScrollLine = 42,
            RestoreSessionOnLaunch = true,
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
        clone.OutlinePlacement.ShouldBe(OutlinePlacement.Left);
        clone.IsSourceSoftWrap.ShouldBeTrue();
        clone.IsPreviewSoftWrap.ShouldBeFalse();
        clone.MermaidScale.ShouldBe(2.5);
        clone.LastOpenedFile.ShouldBe("/tmp/test.md");
        clone.LastScrollLine.ShouldBe(42);
        clone.RestoreSessionOnLaunch.ShouldBeTrue();
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
    public void Serialize_RoundTrip_PreservesAllFields()
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
            OutlinePlacement = OutlinePlacement.Left,
            IsSourceSoftWrap = true,
            IsPreviewSoftWrap = false,
            MermaidScale = 2.5,
            LastOpenedFile = "/tmp/test.md",
            LastScrollLine = 42,
            RestoreSessionOnLaunch = true,
        };
        source.RecentFiles.Add("a.md");
        source.RecentFiles.Add("b.md");

        var json = JsonSerializer.Serialize(source, AppSettingsJsonContext.Default.AppSettings);
        var deserialized = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);

        deserialized.ShouldNotBeNull();
        deserialized.Renderer.ShouldBe(source.Renderer);
        deserialized.Language.ShouldBe(source.Language);
        deserialized.Theme.ShouldBe(source.Theme);
        deserialized.CodeTheme.ShouldBe(source.CodeTheme);
        deserialized.ThemeMode.ShouldBe(source.ThemeMode);
        deserialized.DefaultViewMode.ShouldBe(source.DefaultViewMode);
        deserialized.ShowOutline.ShouldBe(source.ShowOutline);
        deserialized.FontSize.ShouldBe(source.FontSize);
        deserialized.MonoFont.ShouldBe(source.MonoFont);
        deserialized.OutlinePlacement.ShouldBe(source.OutlinePlacement);
        deserialized.IsSourceSoftWrap.ShouldBe(source.IsSourceSoftWrap);
        deserialized.IsPreviewSoftWrap.ShouldBe(source.IsPreviewSoftWrap);
        deserialized.MermaidScale.ShouldBe(source.MermaidScale);
        deserialized.LastOpenedFile.ShouldBe(source.LastOpenedFile);
        deserialized.LastScrollLine.ShouldBe(source.LastScrollLine);
        deserialized.RestoreSessionOnLaunch.ShouldBe(source.RestoreSessionOnLaunch);
        deserialized.RecentFiles.ShouldBe(source.RecentFiles);
    }

    [Fact]
    public void Serialize_UsesCamelCasePropertyNames()
    {
        var sut = new AppSettings();

        var json = JsonSerializer.Serialize(sut, AppSettingsJsonContext.Default.AppSettings);

        json.ShouldContain("\"theme\":", Case.Sensitive);
        json.ShouldNotContain("\"Theme\":", Case.Sensitive);
        json.ShouldContain("\"codeTheme\":", Case.Sensitive);
        json.ShouldNotContain("\"CodeTheme\":", Case.Sensitive);
        json.ShouldContain("\"fontSize\":", Case.Sensitive);
        json.ShouldNotContain("\"FontSize\":", Case.Sensitive);
        json.ShouldContain("\"mermaidScale\":", Case.Sensitive);
        json.ShouldNotContain("\"MermaidScale\":", Case.Sensitive);
        json.ShouldContain("\"lastScrollLine\":", Case.Sensitive);
        json.ShouldNotContain("\"LastScrollLine\":", Case.Sensitive);
    }
}
