namespace Markus.Services.Updates;

internal interface IUpdateLauncher
{
    void OpenArtifact(string localPath);

    void OpenReleasePage(Uri htmlUrl);
}
