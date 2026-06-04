using System.Reflection;
using Markus.Models;

namespace Markus.Services.Updates;

internal sealed class AssemblyVersionProvider : IVersionProvider
{
    public AssemblyVersionProvider()
        : this(ReadInformationalVersion()) { }

    internal AssemblyVersionProvider(string informationalVersion)
    {
        Current = SemVer.TryParse(informationalVersion, out var v) ? v : new SemVer(0, 0, 0, null);
    }

    public SemVer Current { get; }

    private static string ReadInformationalVersion()
    {
        return typeof(AssemblyVersionProvider)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? "0.0.0";
    }
}
