using Markus.Views;

namespace Markus.Tests.Views;

// The grammar and themes ship as assembly manifest resources (NativeAOT trims the
// AvaloniaResource manifest, so avares:// loading fails in a published .app). These
// tests load each shipped resource through the same path the editor uses, catching
// a missing or misnamed embedded resource that a JIT-only smoke test would miss.
public sealed class MarkdownRegistryOptionsTests
{
    public static TheoryData<string> ThemeFiles =>
        new()
        {
            "dark_plus.json",
            "dark_vs.json",
            "light_plus.json",
            "light_vs.json",
            "monokai-color-theme.json",
            "dimmed-monokai-color-theme.json",
            "solarized-light-color-theme.json",
            "solarized-dark-color-theme.json",
            "quietlight-color-theme.json",
            "kimbie-dark-color-theme.json",
            "abyss-color-theme.json",
            "Red-color-theme.json",
            "tomorrow-night-blue-color-theme.json",
        };

    public static TheoryData<string> GrammarScopes =>
        new()
        {
            MarkdownRegistryOptions.MarkdownScope,
            "source.python",
            "source.js",
            "source.ts",
            "source.shell",
            "source.cs",
        };

    [Theory]
    [MemberData(nameof(ThemeFiles))]
    public void EveryShippedTheme_LoadsFromManifest(string themeFile)
    {
        Should.NotThrow(() => MarkdownRegistryOptions.LoadTheme(themeFile)).ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(GrammarScopes))]
    public void EveryShippedGrammar_LoadsFromManifest(string scope)
    {
        var registry = new MarkdownRegistryOptions("dark_plus.json");
        registry.GetGrammar(scope).ShouldNotBeNull();
    }

    [Fact]
    public void Grammar_IsNullForUnsupportedScopes()
    {
        var registry = new MarkdownRegistryOptions("dark_plus.json");
        registry.GetGrammar("source.ruby").ShouldBeNull();
    }

    [Fact]
    public void DefaultTheme_LoadsForConfiguredFile()
    {
        var registry = new MarkdownRegistryOptions("monokai-color-theme.json");
        registry.GetDefaultTheme().ShouldNotBeNull();
    }
}
