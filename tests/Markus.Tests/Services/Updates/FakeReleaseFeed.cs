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

/// <summary>
/// A release feed whose GetReleasesAsync only completes once the supplied
/// gate task does. Tests use this to interleave concurrent writes against
/// the in-flight check.
/// </summary>
internal sealed class GatedReleaseFeed : IReleaseFeed
{
    private readonly Task _gate;
    private readonly IReadOnlyList<ReleaseInfo> _releases;

    public GatedReleaseFeed(Task gate, params ReleaseInfo[] releases)
    {
        _gate = gate;
        _releases = releases;
    }

    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        return _releases;
    }
}
