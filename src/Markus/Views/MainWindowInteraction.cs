using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Markus.ViewModels;

namespace Markus.Views;

/// <summary>
/// Bridges <see cref="IDocumentInteraction"/> to the main window's modal
/// dialogs and storage provider. Kept separate from the window so the window's
/// many private members don't tangle with the interface's public surface.
/// </summary>
internal sealed class MainWindowInteraction : IDocumentInteraction
{
    private readonly Window _owner;

    public MainWindowInteraction(Window owner)
    {
        _owner = owner;
    }

    public Task<UnsavedChangesChoice> ConfirmDiscardAsync(string documentTitle)
    {
        return ConfirmDialog.AskDiscardAsync(_owner, documentTitle);
    }

    public Task<bool> ConfirmReloadAsync(string documentTitle)
    {
        return ConfirmDialog.AskReloadAsync(_owner, documentTitle);
    }

    public async Task<string?> PickSavePathAsync(string suggestedFileName)
    {
        var options = new FilePickerSaveOptions
        {
            Title = "Save Markdown file",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "md",
            FileTypeChoices = new FilePickerFileType[]
            {
                new FilePickerFileType("Markdown") { Patterns = new[] { "*.md", "*.markdown", "*.mdown", "*.mkd" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            },
        };

        var file = await _owner.StorageProvider.SaveFilePickerAsync(options);
        var path = file?.TryGetLocalPath();
        return string.IsNullOrEmpty(path) ? null : path;
    }
}
