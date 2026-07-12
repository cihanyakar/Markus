using System.Diagnostics;

namespace Markus.Services.Updates;

internal sealed class UpdateLauncher : IUpdateLauncher
{
    public void OpenArtifact(string localPath)
    {
        _ = TryOpenArtifact(localPath);
    }

    public bool TryOpenArtifact(string localPath)
    {
        if (OperatingSystem.IsMacOS())
        {
            // The disk image was fetched over HttpClient and carries no
            // quarantine of its own, so macOS would open it without ever
            // checking its signature. Ask Gatekeeper to assess the image's
            // notarization directly before opening. A stapled, notarized build
            // passes this offline, and a tampered image is rejected so we fall
            // back to the release page instead of opening an unassessed
            // download. The checksum check in UpdateDownloader already proved
            // transfer integrity; this adds the publisher-authenticity check the
            // same-origin checksum cannot.
            if (!MayLaunchAfterAssessment(AssessWithGatekeeper(localPath)))
            {
                return false;
            }
            return StartProcess("/usr/bin/open", localPath);
        }

        return Shell(localPath);
    }

    public void OpenReleasePage(Uri htmlUrl)
    {
        Shell(htmlUrl.AbsoluteUri);
    }

    // Given the exit code of `spctl --assess`, decide whether launch may
    // proceed. Zero means Gatekeeper accepted the disk image's signature and
    // notarization. Any other code, including the tool failing to start, is
    // treated as a rejection. Extracted as a pure function so the decision is
    // unit-testable without a real signed disk image on the test host.
    internal static bool MayLaunchAfterAssessment(int assessmentExitCode)
    {
        return assessmentExitCode == 0;
    }

    // Runs Gatekeeper's assessment on the disk image and returns the spctl exit
    // code. A negative code signals that spctl could not be started at all,
    // which MayLaunchAfterAssessment treats the same as a rejection.
    private static int AssessWithGatekeeper(string localPath)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = "/usr/sbin/spctl", UseShellExecute = false };
            psi.ArgumentList.Add("--assess");
            psi.ArgumentList.Add("--type");
            psi.ArgumentList.Add("open");
            psi.ArgumentList.Add("--context");
            psi.ArgumentList.Add("context:primary-signature");
            psi.ArgumentList.Add(localPath);
            using var process = Process.Start(psi);
            if (process is null)
            {
                return -1;
            }
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // spctl could not be launched; treat as a rejection so the caller
            // falls back to the release page instead of opening an unassessed
            // download.
            return -1;
        }
        catch (FileNotFoundException)
        {
            // Same posture.
            return -1;
        }
    }

    private static bool Shell(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No shell association available; nothing more we can do here.
            return false;
        }
        catch (FileNotFoundException)
        {
            // Same posture.
            return false;
        }
    }

    private static bool StartProcess(string fileName, string argument)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = fileName, UseShellExecute = false };
            psi.ArgumentList.Add(argument);
            var process = Process.Start(psi);
            // A null process means the launcher never started; report failure so
            // the caller can surface it instead of leaving "Opening installer..."
            // on screen forever.
            return process is not null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Launch failed; report it so the caller can fall back to the
            // release page instead of silently stalling.
            return false;
        }
        catch (FileNotFoundException)
        {
            // Same posture.
            return false;
        }
    }
}
