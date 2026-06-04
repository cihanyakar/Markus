namespace Markus.Services.Updates;

internal static class ReleaseAssetSelector
{
    public static ReleaseAsset? Select(IReadOnlyList<ReleaseAsset> assets, string rid)
    {
        foreach (var asset in assets)
        {
            if (
                asset.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)
                || !asset.Name.Contains(rid, StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            return asset;
        }

        return null;
    }
}
