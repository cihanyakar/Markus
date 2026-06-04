using Markus.Models;

namespace Markus.Services.Updates;

internal static class GitHubReleaseMapper
{
    public static ReleaseInfo? Map(GitHubRelease dto)
    {
        if (dto.Draft || string.IsNullOrWhiteSpace(dto.TagName))
        {
            return null;
        }

        if (!SemVer.TryParse(dto.TagName, out var version))
        {
            return null;
        }

        if (!Uri.TryCreate(dto.HtmlUrl, UriKind.Absolute, out var htmlUrl))
        {
            return null;
        }

        var assets = new List<ReleaseAsset>();
        foreach (var raw in dto.Assets ?? new List<GitHubAsset>())
        {
            if (
                string.IsNullOrWhiteSpace(raw.Name)
                || !Uri.TryCreate(raw.BrowserDownloadUrl, UriKind.Absolute, out var url)
            )
            {
                continue;
            }

            assets.Add(
                new ReleaseAsset
                {
                    Name = raw.Name,
                    DownloadUrl = url,
                    Size = raw.Size,
                }
            );
        }

        return new ReleaseInfo
        {
            Version = version,
            TagName = dto.TagName,
            IsPrerelease = dto.Prerelease,
            Notes = dto.Body,
            HtmlUrl = htmlUrl,
            Assets = assets,
        };
    }
}
