using Markus.Services.Updates;

namespace Markus.Tests.ViewModels.Updates;

#pragma warning disable SA1402, MA0048 // Three paired fakes intentionally co-located, matching ReleaseModels.cs precedent.

internal sealed class FakeUpdateDownloader : IUpdateDownloader
{
    public ReleaseAsset? DownloadedAsset { get; private set; }

    public string ReturnPath { get; set; } = "/tmp/Markus-update.dmg";

    public Task<string> DownloadAndVerifyAsync(ReleaseAsset asset, string targetDir, CancellationToken ct)
    {
        DownloadedAsset = asset;
        return Task.FromResult(ReturnPath);
    }
}

internal sealed class FakeUpdateLauncher : IUpdateLauncher
{
    public string? OpenedArtifact { get; private set; }

    public Uri? OpenedReleasePage { get; private set; }

    public void OpenArtifact(string localPath)
    {
        OpenedArtifact = localPath;
    }

    public void OpenReleasePage(Uri htmlUrl)
    {
        OpenedReleasePage = htmlUrl;
    }
}

internal sealed class FixedVersionProvider : IVersionProvider
{
    public FixedVersionProvider(string version)
    {
        Current = Markus.Models.SemVer.Parse(version);
    }

    public Markus.Models.SemVer Current { get; }
}

#pragma warning restore SA1402, MA0048
