namespace Markus.Services.Updates;

internal static class ReleaseAssetSelector
{
    // Sidecar / metadata extensions that share the asset filename prefix but
    // are not the installer themselves: checksums (.sha256), code-signing
    // detached signatures (.asc, .sig), electron-updater style metadata
    // (.yml/.yaml, .json, .blockmap), and human-readable companions
    // (.md, .txt). Adding to this list narrows the matcher without breaking
    // existing canonical artifacts.
    private static readonly string[] SidecarSuffixes =
    [
        ".sha256",
        ".blockmap",
        ".yml",
        ".yaml",
        ".json",
        ".md",
        ".txt",
        ".asc",
        ".sig",
    ];

    // Native installer/package extensions ordered by platform precedence
    // within each OS. A release that publishes ANY asset with one of these
    // extensions (matching the rid) is treated as canonical. Anything else
    // (.zip, .tar.gz portable archives) is a fallback chosen only when no
    // installer exists for this rid, so a portable archive cannot silently
    // beat the canonical installer on a tie-break.
    private static readonly string[] InstallerSuffixes = [".dmg", ".pkg", ".msi", ".exe", ".AppImage", ".deb", ".rpm"];

    public static ReleaseAsset? Select(IReadOnlyList<ReleaseAsset> assets, string rid)
    {
        ReleaseAsset? bestInstaller = null;
        ReleaseAsset? bestFallback = null;
        foreach (var asset in assets)
        {
            if (!asset.Name.Contains(rid, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (IsSidecar(asset.Name))
            {
                continue;
            }
            if (IsInstaller(asset.Name))
            {
                // Multiple installer candidates (debug vs canonical, multiple
                // packaging formats) tie-break by SHORTEST filename — the
                // canonical name is `Markus-<version>-<rid>.<ext>` and any
                // decorated variant adds bytes.
                if (bestInstaller is null || asset.Name.Length < bestInstaller.Name.Length)
                {
                    bestInstaller = asset;
                }
            }
            else if (bestFallback is null || asset.Name.Length < bestFallback.Name.Length)
            {
                bestFallback = asset;
            }
        }
        return bestInstaller ?? bestFallback;
    }

    private static bool IsSidecar(string name)
    {
        return SidecarSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsInstaller(string name)
    {
        return InstallerSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
