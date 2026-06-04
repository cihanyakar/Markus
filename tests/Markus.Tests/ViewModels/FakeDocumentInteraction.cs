using Markus.ViewModels;

namespace Markus.Tests.ViewModels;

internal sealed class FakeDocumentInteraction : IDocumentInteraction
{
    public UnsavedChangesChoice DiscardChoice { get; set; } = UnsavedChangesChoice.Discard;

    public bool ReloadConfirm { get; set; } = true;

    public string? SavePathToReturn { get; set; }

    public int DiscardCalls { get; private set; }

    public int ReloadCalls { get; private set; }

    public int PickSaveCalls { get; private set; }

    public string? LastSuggestedName { get; private set; }

    public Task<UnsavedChangesChoice> ConfirmDiscardAsync(string documentTitle)
    {
        DiscardCalls++;
        return Task.FromResult(DiscardChoice);
    }

    public Task<bool> ConfirmReloadAsync(string documentTitle)
    {
        ReloadCalls++;
        return Task.FromResult(ReloadConfirm);
    }

    public Task<string?> PickSavePathAsync(string suggestedFileName)
    {
        PickSaveCalls++;
        LastSuggestedName = suggestedFileName;
        return Task.FromResult(SavePathToReturn);
    }
}
