namespace Markus.Services.Updates;

internal static class UpdatePolicy
{
    public static bool ShouldAutoCheck(
        bool enabled,
        bool isSpawnedChild,
        DateTimeOffset? lastCheckUtc,
        DateTimeOffset now,
        TimeSpan minInterval
    )
    {
        if (!enabled || isSpawnedChild)
        {
            return false;
        }

        if (lastCheckUtc is null)
        {
            return true;
        }

        // A future-dated last-check (clock skew from a previous run, restored
        // backup from a different machine, manual JSON edit) would otherwise
        // suppress auto-check indefinitely until real time overtook the bogus
        // timestamp. Treat anything ahead of `now` as stale and resync.
        if (lastCheckUtc.Value > now)
        {
            return true;
        }

        return now - lastCheckUtc.Value >= minInterval;
    }
}
