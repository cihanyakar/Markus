using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;

namespace Markus.Benchmarks;

// Compares the original digest verification (compute hash, format as hex, then
// ordinal string compare) against the current path (decode the expected hex
// once, hash into a stack buffer, then constant-time compare). The new path
// removes the per-call hex-string allocation and the timing side channel.
[MemoryDiagnoser]
public class Sha256Benchmarks
{
    private byte[] _payload = [];
    private string _expectedHex = string.Empty;

    [Params(1_024, 1_048_576)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _payload = new byte[PayloadBytes];
        new Random(20260606).NextBytes(_payload);
        _expectedHex = Convert.ToHexStringLower(SHA256.HashData(_payload));
    }

    [Benchmark(Baseline = true)]
    public bool Old() => MatchesOld(_expectedHex, _payload);

    [Benchmark]
    public bool New() => MatchesNew(_expectedHex, _payload);

    // Original implementation: compute the hash, allocate its lowercase hex
    // string, then compare ordinally (case-insensitive).
    private static bool MatchesOld(string expectedHex, byte[] fileBytes)
    {
        var actualHex = Convert.ToHexStringLower(SHA256.HashData(fileBytes));
        return string.Equals(expectedHex.Trim(), actualHex, StringComparison.OrdinalIgnoreCase);
    }

    // Current implementation: decode the expected digest, hash into a stack
    // buffer, then compare the raw bytes in constant time.
    private static bool MatchesNew(string expectedHex, byte[] fileBytes)
    {
        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHex.AsSpan().Trim());
        }
        catch (FormatException)
        {
            return false;
        }
        Span<byte> actual = stackalloc byte[32];
        SHA256.HashData(fileBytes, actual);
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
