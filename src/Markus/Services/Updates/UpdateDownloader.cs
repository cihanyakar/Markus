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
        var localPath = ResolveSafeLocalPath(targetDir, asset.Name);

        var bytes = await _http.GetByteArrayAsync(asset.DownloadUrl, ct).ConfigureAwait(false);

        var sidecarUrl = new Uri(asset.DownloadUrl.AbsoluteUri + ".sha256");
        // The downloaded artifact gets launched as an executable. Without a
        // published checksum we cannot prove integrity, so refuse to launch it;
        // the caller falls back to opening the release page in a browser.
        var expected =
            await TryGetExpectedHashAsync(sidecarUrl, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"No published checksum for {asset.Name}; refusing to launch unverified."
            );

        if (!Sha256Verifier.Matches(expected, bytes))
        {
            throw new InvalidOperationException($"Checksum mismatch for {asset.Name}.");
        }

        await File.WriteAllBytesAsync(localPath, bytes, ct).ConfigureAwait(false);
        return localPath;
    }

    // The asset name comes from the GitHub release API and is attacker
    // influenceable, so strip any directory components and confirm the result
    // stays inside targetDir before it is used as a write destination.
    internal static string ResolveSafeLocalPath(string targetDir, string assetName)
    {
        var fileName = Path.GetFileName(assetName);
        if (string.IsNullOrEmpty(fileName) || fileName is "." or "..")
        {
            throw new InvalidOperationException($"Unsafe update asset name: '{assetName}'.");
        }
        var root = Path.GetFullPath(targetDir);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, fileName));
        if (!full.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Update asset would write outside the target directory: '{assetName}'."
            );
        }
        return full;
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
