namespace Markus.Services.Updates;

internal interface IReleaseFeed
{
    Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken ct);
}
