using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Markus.Views;

/// <summary>
/// Runs a callback whenever a control may have just become effectively visible.
/// The view-mode panes defer expensive work while hidden (TextMate install,
/// preview render). The wake-up has to watch IsVisible on the control AND its
/// ancestors: the Source/Preview-only panes flip their own IsVisible, the split
/// panes sit in Grids that flip theirs, and the initial window Show() flips the
/// Window's. EffectiveViewportChanged is not a usable signal here — it does not
/// fire on an own-IsVisible flip, and on a container flip the pane's viewport
/// rect can be unchanged so no event is raised. Avalonia's
/// IsEffectivelyVisibleChanged event would be exactly right but is internal, so
/// this watches IsVisible changes class-wide and filters to the control's
/// ancestor chain. Callbacks must re-check IsEffectivelyVisible themselves.
/// </summary>
internal static class VisibilityActivation
{
    public static IDisposable Subscribe(Control control, Action mayHaveBecomeVisible)
    {
        return Visual.IsVisibleProperty.Changed.AddClassHandler<Visual>(
            (sender, e) =>
            {
                if (!e.GetNewValue<bool>())
                {
                    return;
                }
                if (sender == control || sender.IsVisualAncestorOf(control))
                {
                    mayHaveBecomeVisible();
                }
            }
        );
    }
}
