using System.Security.Cryptography;

namespace Markus.Services.Updates;

internal static class Sha256Verifier
{
    private static readonly char[] Whitespace = [' ', '\t', '\r', '\n'];

    public static string ComputeHex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    public static bool Matches(string expectedHex, byte[] fileBytes)
    {
        // Decode the expected digest and compare against the computed hash in
        // constant time, avoiding the computed-hash hex-string allocation (and
        // the timing side channel of an ordinal string comparison).
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

    // Sidecar files written by the release workflow look like
    // "<hash>  <filename>" (shasum/sha256sum format). Some tools emit just
    // the hash. Take the first whitespace-delimited token.
    public static string? ParseExpectedHash(string sidecarContent)
    {
        if (string.IsNullOrWhiteSpace(sidecarContent))
        {
            return null;
        }

        var tokens = sidecarContent
            .Trim()
            .Split(Whitespace, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return null;
        }
        // Only accept a real 64-char hex digest. A soft error page or malformed
        // sidecar would otherwise be taken as the "expected" hash and reject a
        // perfectly good download.
        return IsSha256Hex(tokens[0]) ? tokens[0] : null;
    }

    private static bool IsSha256Hex(string value)
    {
        return value.Length == 64 && value.All(Uri.IsHexDigit);
    }
}
