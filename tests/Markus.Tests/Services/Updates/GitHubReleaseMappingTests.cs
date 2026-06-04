using System.Text.Json;
using Markus.Services.Updates;

namespace Markus.Tests.Services.Updates;

public sealed class GitHubReleaseMappingTests
{
    private const string Json = """
        {
          "tag_name": "v0.5.0",
          "name": "0.5.0",
          "body": "notes here",
          "draft": false,
          "prerelease": false,
          "html_url": "https://github.com/cihanyakar/Markus/releases/tag/v0.5.0",
          "assets": [
            {
              "name": "Markus-v0.5.0-osx-arm64.dmg",
              "browser_download_url": "https://github.com/cihanyakar/Markus/releases/download/v0.5.0/Markus-v0.5.0-osx-arm64.dmg",
              "size": 12345
            }
          ]
        }
        """;

    [Fact]
    public void Map_ParsesReleaseAndAsset()
    {
        var info = GitHubReleaseMapper.Map(Deserialize(Json));

        info.ShouldNotBeNull();
        info!.TagName.ShouldBe("v0.5.0");
        info.Version.ShouldBe(Markus.Models.SemVer.Parse("0.5.0"));
        info.IsPrerelease.ShouldBeFalse();
        info.Notes.ShouldBe("notes here");
        info.Assets.Count.ShouldBe(1);
        info.Assets[0].Name.ShouldBe("Markus-v0.5.0-osx-arm64.dmg");
        info.Assets[0].Size.ShouldBe(12345);
    }

    [Fact]
    public void Map_DropsDraft()
    {
        var dto = Deserialize(Json);
        dto.Draft = true;

        GitHubReleaseMapper.Map(dto).ShouldBeNull();
    }

    [Fact]
    public void Map_DropsUnparseableTag()
    {
        var dto = Deserialize(Json);
        dto.TagName = "nightly";

        GitHubReleaseMapper.Map(dto).ShouldBeNull();
    }

    private static GitHubRelease Deserialize(string json)
    {
        return JsonSerializer.Deserialize(json, GitHubReleaseJsonContext.Default.GitHubRelease)!;
    }
}
