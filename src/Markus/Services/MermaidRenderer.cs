using System.Diagnostics;

namespace Markus.Services;

internal static class MermaidRenderer
{
    private static readonly string? MmdrPath = FindMmdr();

    public static bool IsAvailable => MmdrPath is not null;

    public static async Task<string?> RenderToSvgAsync(string mermaidSource, CancellationToken ct = default)
    {
        if (MmdrPath is null)
        {
            return null;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = MmdrPath,
                ArgumentList = { "-e", "svg" },
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();

        await process.StandardInput.WriteAsync(mermaidSource.AsMemory(), ct);
        process.StandardInput.Close();

        var svg = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(svg) ? null : svg;
    }

    private static string? FindMmdr()
    {
        var appDir = AppContext.BaseDirectory;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(appDir, "mmdr"),
            Path.Combine(home, ".cargo", "bin", "mmdr"),
            "/opt/homebrew/bin/mmdr",
            "/usr/local/bin/mmdr",
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
