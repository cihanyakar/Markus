namespace Markus.Services;

// Temporary, hand-rolled composition root.
// Will be replaced with Microsoft.Extensions.DependencyInjection in the
// upcoming Markus.Core / Markus.Rendering split (see docs/plan.md).
internal static class ServiceLocator
{
    private static readonly Lazy<SettingsService> _settings = new Lazy<SettingsService>(() => new SettingsService());
    private static readonly Lazy<KeyBindingService> _keys = new Lazy<KeyBindingService>(() =>
        new KeyBindingService(Settings.SettingsDirectory)
    );

    public static SettingsService Settings => _settings.Value;

    public static KeyBindingService Keys => _keys.Value;
}
