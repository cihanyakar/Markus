using Markus.Services.Updates;

namespace Markus.Tests.Services.Updates;

public sealed class UpdateLauncherTests
{
    [Fact]
    public void MayLaunchAfterAssessment_AllowsWhenGatekeeperAccepts()
    {
        // Exit code 0 means spctl accepted the disk image's signature and
        // notarization, so it is safe to open.
        UpdateLauncher.MayLaunchAfterAssessment(0).ShouldBeTrue();
    }

    [Theory]
    [InlineData(3)] // spctl rejected the assessment
    [InlineData(1)] // other non-zero from a failed invocation
    [InlineData(-1)] // spctl could not be started at all
    public void MayLaunchAfterAssessment_RefusesWhenGatekeeperRejects(int exitCode)
    {
        // A non-zero code means the image is not a properly notarized build, so
        // opening it would trust an unassessed download.
        UpdateLauncher.MayLaunchAfterAssessment(exitCode).ShouldBeFalse();
    }
}
