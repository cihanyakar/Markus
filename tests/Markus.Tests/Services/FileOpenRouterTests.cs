namespace Markus.Tests.Services;

public sealed class FileOpenRouterTests
{
    // --- DecideOpen ---

    [Fact]
    public void DecideOpen_SamePath_FocusesExisting()
    {
        var decision = FileOpenRouter.DecideOpen(
            hasInitialDoc: true,
            currentPath: "/docs/readme.md",
            requestedPath: "/docs/readme.md"
        );

        decision.ShouldBe(FileOpenDecision.FocusExisting);
    }

    [Fact]
    public void DecideOpen_SamePathDifferentCase_FocusesExisting()
    {
        var decision = FileOpenRouter.DecideOpen(
            hasInitialDoc: true,
            currentPath: "/Docs/ReadMe.md",
            requestedPath: "/docs/readme.md"
        );

        decision.ShouldBe(FileOpenDecision.FocusExisting);
    }

    [Fact]
    public void DecideOpen_NoDocumentYet_LoadsInCurrent()
    {
        var decision = FileOpenRouter.DecideOpen(
            hasInitialDoc: false,
            currentPath: null,
            requestedPath: "/docs/readme.md"
        );

        decision.ShouldBe(FileOpenDecision.LoadInCurrent);
    }

    [Fact]
    public void DecideOpen_DifferentFileWithDocument_Spawns()
    {
        var decision = FileOpenRouter.DecideOpen(
            hasInitialDoc: true,
            currentPath: "/docs/readme.md",
            requestedPath: "/docs/changelog.md"
        );

        decision.ShouldBe(FileOpenDecision.SpawnNewInstance);
    }

    // --- ShouldRestoreSession ---

    [Fact]
    public void ShouldRestoreSession_FreshLaunchEnabledFileExists_True()
    {
        FileOpenRouter
            .ShouldRestoreSession(isSpawnedChild: false, restoreEnabled: true, lastFileExists: true)
            .ShouldBeTrue();
    }

    [Fact]
    public void ShouldRestoreSession_SpawnedChild_FalseEvenWhenEnabled()
    {
        FileOpenRouter
            .ShouldRestoreSession(isSpawnedChild: true, restoreEnabled: true, lastFileExists: true)
            .ShouldBeFalse();
    }

    [Fact]
    public void ShouldRestoreSession_Disabled_False()
    {
        FileOpenRouter
            .ShouldRestoreSession(isSpawnedChild: false, restoreEnabled: false, lastFileExists: true)
            .ShouldBeFalse();
    }

    [Fact]
    public void ShouldRestoreSession_FileMissing_False()
    {
        FileOpenRouter
            .ShouldRestoreSession(isSpawnedChild: false, restoreEnabled: true, lastFileExists: false)
            .ShouldBeFalse();
    }

    // --- IsSpawnMarker ---

    [Fact]
    public void IsSpawnMarker_WithFlag_True()
    {
        var args = new[] { "--spawned" };

        FileOpenRouter.IsSpawnMarker(args).ShouldBeTrue();
    }

    [Fact]
    public void IsSpawnMarker_WithoutFlag_False()
    {
        var args = new[] { "/some/file.md" };

        FileOpenRouter.IsSpawnMarker(args).ShouldBeFalse();
    }

    [Fact]
    public void IsSpawnMarker_Null_False()
    {
        FileOpenRouter.IsSpawnMarker(null).ShouldBeFalse();
    }
}
