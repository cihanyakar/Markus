using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Themes.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace Markus.Views;

// A minimal IRegistryOptions that ships the markdown grammar, a handful of
// fenced-code-block languages, and the editor color themes Markus exposes. It
// replaces TextMateSharp.Grammars' all-languages RegistryOptions, which bundled
// roughly 6 MB of grammars for 50+ languages. The theme files are pre-resolved at
// build time (the two VS Code "plus" themes have their "include" base merged in),
// so loading needs no include resolution here.
//
// Resources load via the assembly manifest, not Avalonia's avares:// loader:
// NativeAOT trims the AvaloniaResource manifest, so AssetLoader.Open fails in a
// published .app (the same reason IconLoader needs a filesystem fallback).
// Manifest resources survive trimming and work identically under JIT and AOT.
internal sealed class MarkdownRegistryOptions : IRegistryOptions
{
    public const string MarkdownScope = "text.html.markdown";

    // Scopes the markdown grammar embeds for fenced code blocks. Only these load;
    // any other ```language renders as plain text inside the code-block style.
    private static readonly Dictionary<string, string> GrammarFiles = new(StringComparer.Ordinal)
    {
        [MarkdownScope] = "markdown.tmLanguage.json",
        ["source.python"] = "python.tmLanguage.json",
        ["source.js"] = "javascript.tmLanguage.json",
        ["source.ts"] = "typescript.tmLanguage.json",
        ["source.shell"] = "shell.tmLanguage.json",
        ["source.cs"] = "csharp.tmLanguage.json",
    };

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
        if (!GrammarFiles.TryGetValue(scopeName, out var file))
        {
            return null;
        }
        using var stream = OpenResource(file);
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
