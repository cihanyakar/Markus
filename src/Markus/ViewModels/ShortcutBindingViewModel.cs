using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Markus.Services;

namespace Markus.ViewModels;

/// <summary>
/// One row in the Settings → Shortcuts editor: an action plus its current
/// gesture. Two-way bound so editing in the UI flows through
/// <see cref="KeyBindingService"/> and back.
/// </summary>
internal sealed partial class ShortcutBindingViewModel : ObservableObject
{
    private readonly KeyBindingService _service;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GestureDisplay))]
    [NotifyPropertyChangedFor(nameof(IsCustom))]
    private KeyGesture? _gesture;

    [ObservableProperty]
    private string _conflictWarning = string.Empty;

    public ShortcutBindingViewModel(ShortcutAction action, KeyBindingService service)
    {
        Action = action;
        _service = service;
        _gesture = service.GetGesture(action);
    }

    public ShortcutAction Action { get; }

    public string Id => Action.Id;

    public string DisplayName => Action.DisplayName;

    public string Category => Action.Category;

    public string GestureDisplay => Gesture?.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? "—";

    // Differ from default → row gets a "modified" indicator and reset button.
    public bool IsCustom =>
        !ReferenceEquals(Action.DefaultGesture, Gesture)
        && !string.Equals(
            Action.DefaultGesture?.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            Gesture?.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal
        );

    public void Commit(KeyGesture? gesture)
    {
        if (gesture is not null)
        {
            ConflictWarning = _service.FindConflict(Action.Id, gesture) is { } owner
                ? $"Also used by '{owner}'"
                : string.Empty;
        }
        else
        {
            ConflictWarning = string.Empty;
        }
        _service.SetGesture(Action.Id, gesture);
        Gesture = gesture;
    }

    public void Reset()
    {
        _service.ResetGesture(Action.Id);
        Gesture = Action.DefaultGesture;
        ConflictWarning = string.Empty;
    }
}
