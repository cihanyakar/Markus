using System.Diagnostics;

namespace Markus.Services.Updates;

internal sealed class UpdateLauncher : IUpdateLauncher
{
    public void OpenArtifact(string localPath)
    {
        if (OperatingSystem.IsMacOS())
        {
            // `open` mounts a dmg or opens a zip in Finder. The file was fetched
            // by HttpClient, so it carries no quarantine attribute.
            StartProcess("/usr/bin/open", localPath);
            return;
        }

        Shell(localPath);
    }

    public void OpenReleasePage(Uri htmlUrl)
    {
        Shell(htmlUrl.AbsoluteUri);
    }

    private static void Shell(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No shell association available; nothing more we can do here.
        }
        catch (FileNotFoundException)
        {
            // Same posture.
        }
    }

    private static void StartProcess(string fileName, string argument)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = fileName, UseShellExecute = false };
            psi.ArgumentList.Add(argument);
            Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Launch failed; the banner stays so the user can retry or open the
            // release page.
        }
        catch (FileNotFoundException)
        {
            // Same posture.
        }
    }
}
