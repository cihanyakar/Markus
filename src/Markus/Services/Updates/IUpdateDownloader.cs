namespace Markus.Services.Updates;

internal interface IUpdateDownloader
{
    Task<string> DownloadAndVerifyAsync(ReleaseAsset asset, string targetDir, CancellationToken ct);
}
