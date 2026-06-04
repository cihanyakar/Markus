using System.Runtime.InteropServices;
using Markus.Services.Updates;

namespace Markus.Tests.Services.Updates;

public sealed class RuntimeRidTests
{
    [Theory]
    [InlineData("osx", Architecture.Arm64, "osx-arm64")]
    [InlineData("osx", Architecture.X64, "osx-x64")]
    [InlineData("win", Architecture.X64, "win-x64")]
    [InlineData("linux", Architecture.X64, "linux-x64")]
    public void Resolve_BuildsCanonicalRid(string os, Architecture arch, string expected)
    {
        RuntimeRid.Resolve(os, arch).ShouldBe(expected);
    }
}
