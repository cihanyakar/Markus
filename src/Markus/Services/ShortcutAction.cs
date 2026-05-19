using Avalonia.Input;

namespace Markus.Services;

/// <summary>
/// Static description of a user-invokable action plus its default keyboard
/// gesture. Actions are referenced from <see cref="KeyBindingService"/> by
/// stable id so persisted overrides survive UI relabels.
/// </summary>
internal sealed record ShortcutAction(string id, string displayName, string category, KeyGesture? defaultGesture)
{
    public string Id => id;

    public string DisplayName => displayName;

    public string Category => category;

    public KeyGesture? DefaultGesture => defaultGesture;
}
