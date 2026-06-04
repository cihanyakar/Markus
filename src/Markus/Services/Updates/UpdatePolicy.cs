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

        return lastCheckUtc is null || now - lastCheckUtc.Value >= minInterval;
    }
}
