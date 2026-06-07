using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Markus.Services;

// Opt-in startup profiler. Active only when the MARKUS_STARTUP_TRACE environment
// variable is set, so a normal launch pays nothing beyond one boolean check per
// Mark call. It records elapsed-since-first-mark for named phases and dumps them
// to stderr, which lets us measure empty-launch and file-open latency without a
// full GUI benchmark harness.
internal static class StartupTrace
{
    private static readonly long StartTimestamp = Stopwatch.GetTimestamp();
    private static readonly List<(string Phase, double Ms)> Marks = new List<(string, double)>();
    private static readonly Lock Gate = new Lock();

    public static bool IsEnabled { get; } =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MARKUS_STARTUP_TRACE"));

    public static void Mark(string phase)
    {
        if (!IsEnabled)
        {
            return;
        }
        var ms = Stopwatch.GetElapsedTime(StartTimestamp).TotalMilliseconds;
        lock (Gate)
        {
            Marks.Add((phase, ms));
        }
    }

    public static void Dump()
    {
        if (!IsEnabled)
        {
            return;
        }
        var sb = new StringBuilder();
        sb.Append("\n=== Markus startup trace (ms since first mark) ===\n");
        lock (Gate)
        {
            double previous = 0;
            foreach (var (phase, ms) in Marks)
            {
                var at = ms.ToString("F1", CultureInfo.InvariantCulture).PadLeft(9);
                var delta = (ms - previous).ToString("F1", CultureInfo.InvariantCulture).PadLeft(7);
                sb.Append(at).Append("  (+").Append(delta).Append(")  ").Append(phase).Append('\n');
                previous = ms;
            }
        }
        Console.Error.Write(sb.ToString());
        Console.Error.Flush();
    }
}
