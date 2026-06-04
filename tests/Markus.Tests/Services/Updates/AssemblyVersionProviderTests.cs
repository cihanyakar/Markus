using Markus.Models;
using Markus.Services.Updates;

namespace Markus.Tests.Services.Updates;

public sealed class AssemblyVersionProviderTests
{
    [Fact]
    public void Current_ParsesInformationalVersion()
    {
        var provider = new AssemblyVersionProvider("0.4.1-alpha.0.5+abc123");

        provider.Current.ShouldBe(SemVer.Parse("0.4.1-alpha.0.5"));
    }

    [Fact]
    public void Current_FallsBackToZeroOnGarbage()
    {
        var provider = new AssemblyVersionProvider("not-a-version");

        provider.Current.ShouldBe(new SemVer(0, 0, 0, null));
    }
}
