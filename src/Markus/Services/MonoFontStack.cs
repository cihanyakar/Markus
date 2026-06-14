namespace Markus.Services;

internal static class MonoFontStack
{
    // System-only monospace chain. Menlo ships with every macOS release
    // since 10.6, Consolas ships with every Windows release since Vista,
    // and ui-monospace + the generic `monospace` cover Linux and anything
    // exotic. Avoid bundled or popular-but-optional families (JetBrains
    // Mono, Iosevka, Cascadia Code) so the editor never depends on the
    // user having installed something extra.
    public static string Build(string requested)
    {
        return $"{requested},Menlo,Consolas,ui-monospace,monospace";
    }
}
