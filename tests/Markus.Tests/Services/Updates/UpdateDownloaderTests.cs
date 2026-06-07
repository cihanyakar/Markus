using System.Net;
using System.Text;
using Markus.Services.Updates;

namespace Markus.Tests.Services.Updates;

public sealed class UpdateDownloaderTests
{
    private const string PayloadHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"; // sha256("abc")

    [Fact]
    public void ResolveSafeLocalPath_KeepsPlainFileNameInsideTarget()
    {
        var dir = Path.Combine(Path.GetTempPath(), "markus-test-target");
        var path = UpdateDownloader.ResolveSafeLocalPath(dir, "Markus-v1.zip");

        Path.GetFileName(path).ShouldBe("Markus-v1.zip");
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(dir), StringComparison.Ordinal).ShouldBeTrue();
    }

    [Theory]
    [InlineData("../evil.sh")] // relative traversal
    [InlineData("a/b/evil.sh")] // nested path
    [InlineData("/etc/passwd")] // absolute path
    public void ResolveSafeLocalPath_NeutralizesPathComponents(string assetName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "markus-test-target");
        var path = UpdateDownloader.ResolveSafeLocalPath(dir, assetName);

        // Whatever the input, the result is a plain file directly under the target.
        Path.GetDirectoryName(path).ShouldBe(Path.GetFullPath(dir));
        Path.GetFileName(path).ShouldBe(Path.GetFileName(assetName));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    public void ResolveSafeLocalPath_RejectsEmptyOrDotNames(string assetName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "markus-test-target");

        Should.Throw<InvalidOperationException>(() => UpdateDownloader.ResolveSafeLocalPath(dir, assetName));
    }

    [Fact]
    public async Task DownloadAndVerify_ThrowsWhenChecksumSidecarMissing()
    {
        var downloader = new UpdateDownloader(NewClient(payload: "abc", sidecarStatus: HttpStatusCode.NotFound));
        var asset = new ReleaseAsset { Name = "Markus.zip", DownloadUrl = new Uri("https://example.com/Markus.zip") };

        await Should.ThrowAsync<InvalidOperationException>(() =>
            downloader.DownloadAndVerifyAsync(asset, NewTargetDir(), CancellationToken.None)
        );
    }

    [Fact]
    public async Task DownloadAndVerify_ThrowsOnChecksumMismatch()
    {
        var downloader = new UpdateDownloader(NewClient(payload: "abc", sidecarBody: new string('0', 64)));
        var asset = new ReleaseAsset { Name = "Markus.zip", DownloadUrl = new Uri("https://example.com/Markus.zip") };

        await Should.ThrowAsync<InvalidOperationException>(() =>
            downloader.DownloadAndVerifyAsync(asset, NewTargetDir(), CancellationToken.None)
        );
    }

    [Fact]
    public async Task DownloadAndVerify_WritesFileWhenChecksumMatches()
    {
        var dir = NewTargetDir();
        var downloader = new UpdateDownloader(NewClient(payload: "abc", sidecarBody: PayloadHash));
        var asset = new ReleaseAsset { Name = "Markus.zip", DownloadUrl = new Uri("https://example.com/Markus.zip") };

        var path = await downloader.DownloadAndVerifyAsync(asset, dir, CancellationToken.None);

        File.Exists(path).ShouldBeTrue();
        (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).ShouldBe("abc");
    }

    private static string NewTargetDir()
    {
        return Path.Combine(Path.GetTempPath(), "markus-dl-" + Path.GetRandomFileName());
    }

    private static HttpClient NewClient(
        string payload,
        string? sidecarBody = null,
        HttpStatusCode sidecarStatus = HttpStatusCode.OK
    )
    {
        return new HttpClient(new StubHandler(payload, sidecarBody, sidecarStatus));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _payload;
        private readonly string? _sidecarBody;
        private readonly HttpStatusCode _sidecarStatus;

        public StubHandler(string payload, string? sidecarBody, HttpStatusCode sidecarStatus)
        {
            _payload = payload;
            _sidecarBody = sidecarBody;
            _sidecarStatus = sidecarStatus;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var isSidecar = request.RequestUri!.AbsoluteUri.EndsWith(".sha256", StringComparison.Ordinal);
            if (isSidecar)
            {
                var resp = new HttpResponseMessage(_sidecarStatus);
                if (_sidecarStatus == HttpStatusCode.OK)
                {
                    resp.Content = new StringContent(_sidecarBody ?? string.Empty, Encoding.ASCII);
                }
                return Task.FromResult(resp);
            }
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_payload, Encoding.ASCII) }
            );
        }
    }
}
