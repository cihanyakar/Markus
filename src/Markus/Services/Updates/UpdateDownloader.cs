namespace Markus.Services.Updates;

internal sealed class UpdateDownloader : IUpdateDownloader
{
    private readonly HttpClient _http;

    public UpdateDownloader()
        : this(CreateClient()) { }

    internal UpdateDownloader(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> DownloadAndVerifyAsync(ReleaseAsset asset, string targetDir, CancellationToken ct)
    {
        Directory.CreateDirectory(targetDir);
        var localPath = Path.Combine(targetDir, asset.Name);

        var bytes = await _http.GetByteArrayAsync(asset.DownloadUrl, ct).ConfigureAwait(false);

        var sidecarUrl = new Uri(asset.DownloadUrl.AbsoluteUri + ".sha256");
        var expected = await TryGetExpectedHashAsync(sidecarUrl, ct).ConfigureAwait(false);

        if (expected is not null && !Sha256Verifier.Matches(expected, bytes))
        {
            throw new InvalidOperationException($"Checksum mismatch for {asset.Name}.");
        }

        await File.WriteAllBytesAsync(localPath, bytes, ct).ConfigureAwait(false);
        return localPath;
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var version = typeof(UpdateDownloader).Assembly.GetName().Version?.ToString(3) ?? "dev";
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Markus/{version}");
        return http;
    }

    private async Task<string?> TryGetExpectedHashAsync(Uri sidecarUrl, CancellationToken ct)
    {
        try
        {
            var content = await _http.GetStringAsync(sidecarUrl, ct).ConfigureAwait(false);
            return Sha256Verifier.ParseExpectedHash(content);
        }
        catch (HttpRequestException)
        {
            // No sidecar published. Proceed without verification rather than
            // blocking the user; the asset still came from the release.
            return null;
        }
    }
}
