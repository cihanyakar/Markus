using Markus.Models;

namespace Markus.Tests.Models;

public sealed class SemVerTests
{
    [Theory]
    [InlineData("0.4.0", 0, 4, 0, null)]
    [InlineData("v0.4.0", 0, 4, 0, null)]
    [InlineData("1.2.3-alpha.0.5", 1, 2, 3, "alpha.0.5")]
    [InlineData("0.4.1-alpha.0.5+abc123", 0, 4, 1, "alpha.0.5")]
    public void Parse_ExtractsComponents(string text, int major, int minor, int patch, string? pre)
    {
        var ok = SemVer.TryParse(text, out var v);

        ok.ShouldBeTrue();
        v.Major.ShouldBe(major);
        v.Minor.ShouldBe(minor);
        v.Patch.ShouldBe(patch);
        v.PreRelease.ShouldBe(pre);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.2")]
    [InlineData("1.2.x")]
    public void TryParse_RejectsGarbage(string text)
    {
        SemVer.TryParse(text, out _).ShouldBeFalse();
    }

    [Fact]
    public void Compare_HigherCoreIsGreater()
    {
        SemVer.Parse("0.5.0").CompareTo(SemVer.Parse("0.4.9")).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Compare_PrereleaseIsLessThanRelease()
    {
        SemVer.Parse("1.0.0-alpha").CompareTo(SemVer.Parse("1.0.0")).ShouldBeLessThan(0);
    }

    [Fact]
    public void Compare_PrereleaseOrderingFollowsSemver()
    {
        SemVer.Parse("1.0.0-alpha.1").CompareTo(SemVer.Parse("1.0.0-alpha.2")).ShouldBeLessThan(0);
        SemVer.Parse("1.0.0-alpha.2").CompareTo(SemVer.Parse("1.0.0-alpha.10")).ShouldBeLessThan(0);
        SemVer.Parse("1.0.0-beta").CompareTo(SemVer.Parse("1.0.0-alpha")).ShouldBeGreaterThan(0);
        SemVer.Parse("1.0.0-alpha.1").CompareTo(SemVer.Parse("1.0.0-alpha.beta")).ShouldBeLessThan(0);
        SemVer.Parse("1.0.0-alpha").CompareTo(SemVer.Parse("1.0.0-alpha.1")).ShouldBeLessThan(0);
    }

    [Fact]
    public void Compare_BuildMetadataIgnored()
    {
        SemVer.Parse("1.0.0+a").CompareTo(SemVer.Parse("1.0.0+b")).ShouldBe(0);
    }

    [Fact]
    public void Parse_ThrowsOnGarbage()
    {
        Should.Throw<FormatException>(() => SemVer.Parse("abc"));
    }

    [Theory]
    [InlineData("01.0.0")]
    [InlineData("1.0.0-alpha.01")]
    public void TryParse_RejectsLeadingZeroNumericIdentifiers(string text)
    {
        SemVer.TryParse(text, out _).ShouldBeFalse();
    }
}
