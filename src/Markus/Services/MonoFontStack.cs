namespace Markus.Services;

internal static class MonoFontStack
{
    public static string Build(string requested)
    {
        return $"{requested},Iosevka,JetBrains Mono,Cascadia Code,Consolas,Menlo,monospace";
    }
}
