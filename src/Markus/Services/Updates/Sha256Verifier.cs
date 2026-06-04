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
        return string.Equals(expectedHex.Trim(), ComputeHex(fileBytes), StringComparison.OrdinalIgnoreCase);
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
        return tokens.Length == 0 ? null : tokens[0];
    }
}
