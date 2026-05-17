namespace Markus.Models;

/// <summary>
/// One entry in the command palette. <see cref="Title"/> is the primary label,
/// <see cref="Group"/> is the small subtitle (e.g. "View" or "Theme"),
/// <see cref="KeyHint"/> is the optional accelerator like "⌘O", and
/// <see cref="Execute"/> performs the command when chosen.
/// </summary>
internal sealed record CommandItem(string title, string group, string? keyHint, Action execute)
{
    public string Title => title;

    public string Group => group;

    public string? KeyHint => keyHint;

    public Action Execute => execute;
}
