using System.Text;
using Markus.Services.Updates;

namespace Markus.Tests.Services.Updates;

public sealed class Sha256VerifierTests
{
    private const string AbcHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void ComputeHex_MatchesKnownVector()
    {
        Sha256Verifier.ComputeHex(Encoding.ASCII.GetBytes("abc")).ShouldBe(AbcHash);
    }

    [Fact]
    public void Matches_IsCaseInsensitive()
    {
        Sha256Verifier.Matches(AbcHash.ToUpperInvariant(), Encoding.ASCII.GetBytes("abc")).ShouldBeTrue();
    }

    [Fact]
    public void Matches_FalseOnTamper()
    {
        Sha256Verifier.Matches(AbcHash, Encoding.ASCII.GetBytes("abcd")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("ba7816bf...  Markus-v0.5.0-osx-arm64.dmg", "ba7816bf...")]
    [InlineData("ba7816bf...", "ba7816bf...")]
    public void ParseExpectedHash_ReadsSidecar(string content, string expected)
    {
        Sha256Verifier.ParseExpectedHash(content).ShouldBe(expected);
    }

    [Fact]
    public void ParseExpectedHash_NullOnEmpty()
    {
        Sha256Verifier.ParseExpectedHash("   ").ShouldBeNull();
    }
}
