using Markus.Models;

namespace Markus.Services.Updates;

#pragma warning disable SA1206 // 'public required' matches csharp_preferred_modifier_order (IDE0036)

internal sealed class ReleaseAsset
{
    public required string Name { get; init; }

    public required Uri DownloadUrl { get; init; }

    public long Size { get; init; }
}

internal sealed class ReleaseInfo
{
    public required SemVer Version { get; init; }

    public required string TagName { get; init; }

    public required Uri HtmlUrl { get; init; }

    public required IReadOnlyList<ReleaseAsset> Assets { get; init; }

    public bool IsPrerelease { get; init; }

    public string? Notes { get; init; }
}

internal sealed class UpdateCheckResult
{
    public static UpdateCheckResult None { get; } = new UpdateCheckResult { UpdateAvailable = false };

    public bool UpdateAvailable { get; init; }

    public ReleaseInfo? Release { get; init; }

    public ReleaseAsset? Asset { get; init; }
}

#pragma warning restore SA1206
