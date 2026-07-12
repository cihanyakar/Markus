namespace Markus.Services.Updates;

internal interface IUpdateLauncher
{
    void OpenArtifact(string localPath);

    void OpenReleasePage(Uri htmlUrl);

    // Launches the installer and reports whether the launch actually started, so
    // the caller can fall back to the release page instead of leaving an
    // "Opening installer..." banner that never resolves. Defaults to the
    // fire-and-forget OpenArtifact for implementations that do not track
    // success (e.g. test doubles).
    bool TryOpenArtifact(string localPath)
    {
        OpenArtifact(localPath);
        return true;
    }
}
