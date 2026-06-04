using System.Text.Json.Serialization;

namespace Markus.Services.Updates;

// Source-generated so NativeAOT does not need the reflection serializer.
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(List<GitHubRelease>))]
internal sealed partial class GitHubReleaseJsonContext : JsonSerializerContext { }
