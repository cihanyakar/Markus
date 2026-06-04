using Markus.Models;

namespace Markus.Services.Updates;

internal sealed class UpdateChecker
{
    private readonly IReleaseFeed _feed;

    public UpdateChecker(IReleaseFeed feed)
    {
        _feed = feed;
    }

    public async Task<UpdateCheckResult> CheckAsync(
        SemVer current,
        UpdateChannel channel,
        string rid,
        CancellationToken ct
    )
    {
        var releases = await _feed.GetReleasesAsync(ct).ConfigureAwait(false);

        ReleaseInfo? best = null;
        foreach (var release in releases)
        {
            if (channel == UpdateChannel.Stable && release.IsPrerelease)
            {
                continue;
            }

            if (best is null || release.Version > best.Version)
            {
                best = release;
            }
        }

        if (best is null || best.Version <= current)
        {
            return UpdateCheckResult.None;
        }

        var asset = ReleaseAssetSelector.Select(best.Assets, rid);
        return new UpdateCheckResult
        {
            UpdateAvailable = true,
            Release = best,
            Asset = asset,
        };
    }
}
