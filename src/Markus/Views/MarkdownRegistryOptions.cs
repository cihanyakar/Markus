using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Themes.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace Markus.Views;

// A minimal IRegistryOptions that ships only the markdown grammar plus the editor
// color themes Markus exposes. It replaces TextMateSharp.Grammars' all-languages
// RegistryOptions, which bundled roughly 6 MB of grammars for 50+ languages the
// editor never highlights. The theme files are pre-resolved at build time (the
// two VS Code "plus" themes have their "include" base merged in), so loading
// needs no include resolution here.
//
// Resources load via the assembly manifest, not Avalonia's avares:// loader:
// NativeAOT trims the AvaloniaResource manifest, so AssetLoader.Open fails in a
// published .app (the same reason IconLoader needs a filesystem fallback).
// Manifest resources survive trimming and work identically under JIT and AOT.
internal sealed class MarkdownRegistryOptions : IRegistryOptions
{
    public const string MarkdownScope = "text.html.markdown";

    private const string GrammarResource = "markdown.tmLanguage.json";

    private readonly string _themeFile;

    public MarkdownRegistryOptions(string themeFile)
    {
        _themeFile = themeFile;
    }

    public static IRawTheme LoadTheme(string themeFile)
    {
        using var stream = OpenResource(themeFile);
        using var reader = new StreamReader(stream);
        return ThemeReader.ReadThemeSync(reader);
    }

    public IRawTheme GetDefaultTheme()
    {
        return LoadTheme(_themeFile);
    }

    public IRawTheme GetTheme(string scopeName)
    {
        return GetDefaultTheme();
    }

    public IRawGrammar? GetGrammar(string scopeName)
    {
        if (!string.Equals(scopeName, MarkdownScope, StringComparison.Ordinal))
        {
            return null;
        }
        using var stream = OpenResource(GrammarResource);
        using var reader = new StreamReader(stream);
        return GrammarReader.ReadGrammarSync(reader);
    }

    public ICollection<string> GetInjections(string scopeName)
    {
        return Array.Empty<string>();
    }

    private static Stream OpenResource(string logicalName)
    {
        var assembly = typeof(MarkdownRegistryOptions).Assembly;
        return assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded TextMate resource missing: {logicalName}");
    }
}
