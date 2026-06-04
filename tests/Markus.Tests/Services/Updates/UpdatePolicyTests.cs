using Markus.Services.Updates;

namespace Markus.Tests.Services.Updates;

public sealed class UpdatePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Day = TimeSpan.FromHours(20);

    [Fact]
    public void NeverCheckedAndEnabled_ChecksOnFreshLaunch()
    {
        UpdatePolicy
            .ShouldAutoCheck(enabled: true, isSpawnedChild: false, lastCheckUtc: null, now: Now, minInterval: Day)
            .ShouldBeTrue();
    }

    [Fact]
    public void Disabled_NeverChecks()
    {
        UpdatePolicy
            .ShouldAutoCheck(enabled: false, isSpawnedChild: false, lastCheckUtc: null, now: Now, minInterval: Day)
            .ShouldBeFalse();
    }

    [Fact]
    public void SpawnedChild_NeverChecks()
    {
        UpdatePolicy
            .ShouldAutoCheck(enabled: true, isSpawnedChild: true, lastCheckUtc: null, now: Now, minInterval: Day)
            .ShouldBeFalse();
    }

    [Fact]
    public void WithinInterval_DoesNotCheck()
    {
        UpdatePolicy.ShouldAutoCheck(true, false, Now.AddHours(-1), Now, Day).ShouldBeFalse();
    }

    [Fact]
    public void PastInterval_Checks()
    {
        UpdatePolicy.ShouldAutoCheck(true, false, Now.AddHours(-21), Now, Day).ShouldBeTrue();
    }
}
