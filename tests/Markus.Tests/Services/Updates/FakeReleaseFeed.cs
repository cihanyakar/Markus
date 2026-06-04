using Markus.Services.Updates;

namespace Markus.Tests.Services.Updates;

internal sealed class FakeReleaseFeed : IReleaseFeed
{
    private readonly IReadOnlyList<ReleaseInfo> _releases;

    public FakeReleaseFeed(params ReleaseInfo[] releases)
    {
        _releases = releases;
    }

    public Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken ct)
    {
        return Task.FromResult(_releases);
    }
}
