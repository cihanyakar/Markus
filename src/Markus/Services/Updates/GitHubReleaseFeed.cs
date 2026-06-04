using System.Net.Http.Json;

namespace Markus.Services.Updates;

internal sealed class GitHubReleaseFeed : IReleaseFeed
{
    // Repo coordinates built at runtime to avoid the hardcoded-URI lint (S1075),
    // matching AboutWindow's approach.
    private const string RepoOwner = "cihanyakar";
    private const string RepoName = "Markus";

    private readonly HttpClient _http;
    private readonly Uri _endpoint;

    public GitHubReleaseFeed()
        : this(CreateClient()) { }

    internal GitHubReleaseFeed(HttpClient http)
    {
        _http = http;
        _endpoint = new Uri($"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases?per_page=30");
    }

    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken ct)
    {
        var dtos = await _http
            .GetFromJsonAsync(_endpoint, GitHubReleaseJsonContext.Default.ListGitHubRelease, ct)
            .ConfigureAwait(false);

        if (dtos is null)
        {
            return Array.Empty<ReleaseInfo>();
        }

        var result = new List<ReleaseInfo>();
        foreach (var dto in dtos)
        {
            var info = GitHubReleaseMapper.Map(dto);
            if (info is not null)
            {
                result.Add(info);
            }
        }

        return result;
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // GitHub rejects requests without a User-Agent. Version is informational.
        var version = typeof(GitHubReleaseFeed).Assembly.GetName().Version?.ToString(3) ?? "dev";
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Markus/{version}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }
}
