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

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // mmdr is missing or not executable; treat as "no diagram" rather
            // than faulting the fire-and-forget render task.
            return null;
        }
        catch (IOException)
        {
            return null;
        }

        return await DrainSvgAsync(process, mermaidSource, ct);
    }

    private static async Task<string?> DrainSvgAsync(Process process, string mermaidSource, CancellationToken ct)
    {
        // Bound a hung mmdr so a malformed diagram can't block the render task
        // forever.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeoutCts.Token;

        try
        {
            // Drain stdout and stderr concurrently with writing stdin: writing
            // the whole source first and only then reading would deadlock once
            // mmdr's output (or stderr) fills its pipe buffer.
            var stdoutTask = process.StandardOutput.ReadToEndAsync(token);
            var stderrTask = process.StandardError.ReadToEndAsync(token);
            await process.StandardInput.WriteAsync(mermaidSource.AsMemory(), token);
            process.StandardInput.Close();

            var svg = await stdoutTask;
            await stderrTask;
            await process.WaitForExitAsync(token);

            if (process.ExitCode != 0)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(svg) ? null : svg;
        }
        finally
        {
            // Disposing the Process handle does not kill the OS process; a
            // cancelled or timed-out mmdr would otherwise be orphaned.
            TryKill(process);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited between the check and the kill.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Kill failed (already dead / permissions); nothing more we can do.
        }
        catch (NotSupportedException)
        {
            // Platform can't kill the tree; best-effort only.
        }
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
