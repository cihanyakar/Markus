namespace Markus.ViewModels;

internal enum UnsavedChangesChoice
{
    // Cancel is the zero value so that a dialog dismissed via the window
    // close button or Escape (which yields default) defaults to the safe,
    // work-preserving outcome.
    Cancel,
    Save,
    Discard,
}

// View-supplied dialogs the view-model needs to drive document flows that
// would otherwise lose unsaved work. Implemented by the main window; left null
// in headless tests, where each guarded flow falls back to proceeding.
internal interface IDocumentInteraction
{
    // Asks how to handle unsaved edits before switching away from the current
    // document (open another, new scratch, close).
    Task<UnsavedChangesChoice> ConfirmDiscardAsync(string documentTitle);

    // Asks whether to replace unsaved edits with the on-disk version, used for
    // explicit reload and for external changes detected on disk.
    Task<bool> ConfirmReloadAsync(string documentTitle);

    // Prompts for a destination path when saving a document that has none.
    // Returns null when the user cancels.
    Task<string?> PickSavePathAsync(string suggestedFileName);
}
