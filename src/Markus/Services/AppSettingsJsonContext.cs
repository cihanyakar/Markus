using System.Text.Json.Serialization;
using Markus.Models;

namespace Markus.Services;

/// <summary>
/// Source-generated JSON metadata for AppSettings. Routing all settings I/O
/// through this context lets PublishTrimmed strip System.Text.Json's
/// reflection-based serializer without breaking persistence.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext { }
