using System.Text.Json.Serialization;

namespace Markus.Services.Updates;

#pragma warning disable SA1402 // GitHubRelease and GitHubAsset are paired by the source generator and live together for cohesion.
#pragma warning disable MA0048 // Same rationale; the file name reflects the pair.
#pragma warning disable CA1002 // List<T> is required by System.Text.Json source generation; types are internal.

internal sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

internal sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

#pragma warning restore CA1002
#pragma warning restore MA0048
#pragma warning restore SA1402
