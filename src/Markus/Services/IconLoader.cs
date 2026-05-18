using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Markus.Services;

/// <summary>
/// Resolves the Markus app icon at runtime. NativeAOT trims away the
/// AvaloniaResource manifest used by XAML's <c>Icon="/Assets/..."</c> sugar,
/// so windows now load their icons through this helper. Tries the canonical
/// avares:// URI first (works under JIT/R2R); on AOT it falls through to a
/// filesystem copy placed under <c>Contents/Resources/</c> by the bundle script.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S1075:URIs should not be hardcoded",
    Justification = "avares:// is an Avalonia in-assembly resource scheme, not a network URI."
)]
internal static class IconLoader
{
    private const string IconUri = "avares://Markus/Assets/markus.png";
    private const string FileName = "markus.png";

    public static WindowIcon? LoadWindowIcon()
    {
        var bitmap = LoadBitmap();
        return bitmap is null ? null : new WindowIcon(bitmap);
    }

    public static Bitmap? LoadBitmap()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(IconUri));
            return new Bitmap(stream);
        }
        catch (System.IO.FileNotFoundException)
        {
            return LoadFromFilesystem();
        }
        catch (UriFormatException)
        {
            return LoadFromFilesystem();
        }
    }

    private static Bitmap? LoadFromFilesystem()
    {
        var path = ResolveFilesystemPath();
        if (path is null)
        {
            return null;
        }
        try
        {
            using var fs = System.IO.File.OpenRead(path);
            return new Bitmap(fs);
        }
        catch (System.IO.IOException)
        {
            return null;
        }
    }

    private static string? ResolveFilesystemPath()
    {
        var baseDir = AppContext.BaseDirectory;
        // macOS .app bundle: .app/Contents/MacOS/Markus → ../Resources/markus.png
        var bundle = System.IO.Path.Combine(baseDir, "..", "Resources", FileName);
        if (System.IO.File.Exists(bundle))
        {
            return System.IO.Path.GetFullPath(bundle);
        }
        // Dev / single-file: alongside the executable.
        var local = System.IO.Path.Combine(baseDir, FileName);
        return System.IO.File.Exists(local) ? local : null;
    }
}
