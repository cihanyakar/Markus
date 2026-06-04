using System.Runtime.InteropServices;

namespace Markus.Services.Updates;

internal static class RuntimeRid
{
    public static string Current => Resolve(CurrentOs(), RuntimeInformation.ProcessArchitecture);

    public static string Resolve(string os, Architecture arch)
    {
        return $"{os}-{Arch(arch)}";
    }

    private static string CurrentOs()
    {
        if (OperatingSystem.IsMacOS())
        {
            return "osx";
        }

        if (OperatingSystem.IsWindows())
        {
            return "win";
        }

        return "linux";
    }

    private static string Arch(Architecture arch)
    {
        return arch == Architecture.Arm64 ? "arm64" : "x64";
    }
}
