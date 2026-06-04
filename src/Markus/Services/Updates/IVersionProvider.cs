using Markus.Models;

namespace Markus.Services.Updates;

internal interface IVersionProvider
{
    SemVer Current { get; }
}
