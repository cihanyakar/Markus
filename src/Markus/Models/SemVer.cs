using System.Globalization;

namespace Markus.Models;

internal readonly struct SemVer : IComparable<SemVer>, IEquatable<SemVer>
{
    public SemVer(int major, int minor, int patch, string? preRelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = string.IsNullOrEmpty(preRelease) ? null : preRelease;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    // null means a stable release. A non-null value is the dot-separated
    // prerelease string, e.g. "alpha.0.5".
    public string? PreRelease { get; }

    public static bool operator ==(SemVer left, SemVer right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SemVer left, SemVer right)
    {
        return !left.Equals(right);
    }

    public static bool operator <(SemVer left, SemVer right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(SemVer left, SemVer right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(SemVer left, SemVer right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(SemVer left, SemVer right)
    {
        return left.CompareTo(right) >= 0;
    }

    public static SemVer Parse(string text)
    {
        return TryParse(text, out var v) ? v : throw new FormatException($"Not a semantic version: {text}");
    }

    public static bool TryParse(string? text, out SemVer value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.Trim();
        if (span.StartsWith('v') || span.StartsWith('V'))
        {
            span = span[1..];
        }

        string? build = null;
        var plus = span.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            build = span[(plus + 1)..];
            span = span[..plus];
        }

        string? pre = null;
        var dash = span.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            pre = span[(dash + 1)..];
            span = span[..dash];
        }

        if (!TryParseCore(span, out var major, out var minor, out var patch))
        {
            return false;
        }

        if (pre is not null && !IsValidPreRelease(pre))
        {
            return false;
        }

        if (build is not null && !IsValidBuildMetadata(build))
        {
            return false;
        }

        value = new SemVer(major, minor, patch, pre);
        return true;
    }

    public int CompareTo(SemVer other)
    {
        var core = Major.CompareTo(other.Major);
        if (core != 0)
        {
            return core;
        }

        core = Minor.CompareTo(other.Minor);
        if (core != 0)
        {
            return core;
        }

        core = Patch.CompareTo(other.Patch);
        if (core != 0)
        {
            return core;
        }

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    public bool Equals(SemVer other)
    {
        return CompareTo(other) == 0;
    }

    public override bool Equals(object? obj)
    {
        return obj is SemVer other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Major, Minor, Patch, PreRelease);
    }

    public override string ToString()
    {
        var core = $"{Major}.{Minor}.{Patch}";
        return PreRelease is null ? core : $"{core}-{PreRelease}";
    }

    private static int ComparePreRelease(string? a, string? b)
    {
        if (a is null && b is null)
        {
            return 0;
        }

        // A version with a prerelease has lower precedence than one without.
        if (a is null)
        {
            return 1;
        }

        if (b is null)
        {
            return -1;
        }

        var left = a.Split('.');
        var right = b.Split('.');
        var max = Math.Max(left.Length, right.Length);
        for (var i = 0; i < max; i++)
        {
            if (i >= left.Length)
            {
                return -1;
            }

            if (i >= right.Length)
            {
                return 1;
            }

            var cmp = CompareIdentifier(left[i], right[i]);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }

    private static int CompareIdentifier(string a, string b)
    {
        var aNum = int.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out var an);
        var bNum = int.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out var bn);

        if (aNum && bNum)
        {
            return an.CompareTo(bn);
        }

        // Numeric identifiers always have lower precedence than alphanumeric.
        if (aNum)
        {
            return -1;
        }

        if (bNum)
        {
            return 1;
        }

        return string.CompareOrdinal(a, b);
    }

    private static bool IsValidPreRelease(string pre)
    {
        foreach (var ident in pre.Split('.'))
        {
            // SemVer 2.0 item 9: identifiers are non-empty and contain only
            // ASCII alphanumerics and hyphens.
            if (ident.Length == 0 || !IsAlphanumericOrHyphen(ident))
            {
                return false;
            }

            // Numeric identifiers must not have leading zeros.
            if (ident.Length > 1 && ident[0] == '0' && AllDigits(ident))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidBuildMetadata(string build)
    {
        // SemVer 2.0 item 10: build identifiers share the prerelease character
        // set but, unlike prerelease, allow numeric identifiers with leading
        // zeros (build metadata is never compared).
        foreach (var ident in build.Split('.'))
        {
            if (ident.Length == 0 || !IsAlphanumericOrHyphen(ident))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAlphanumericOrHyphen(string s)
    {
        return s.All(static c => c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '-');
    }

    private static bool AllDigits(string s)
    {
        return s.All(static c => c is >= '0' and <= '9');
    }

    private static bool TryParseCore(string core, out int major, out int minor, out int patch)
    {
        major = 0;
        minor = 0;
        patch = 0;
        var parts = core.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minor)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out patch)
        )
        {
            return false;
        }

        // Strict SemVer 2.0 disallows leading zeros in numeric identifiers.
        return !parts.Any(static p => p.Length > 1 && p[0] == '0');
    }
}
