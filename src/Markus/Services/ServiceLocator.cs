namespace Markus.Services;

// Temporary, hand-rolled composition root.
// Will be replaced with Microsoft.Extensions.DependencyInjection in the
// upcoming Markus.Core / Markus.Rendering split (see docs/plan.md).
internal static class ServiceLocator
{
    private static readonly Lazy<SettingsService> _settings = new Lazy<SettingsService>(() => new SettingsService());

    public static SettingsService Settings => _settings.Value;
}
