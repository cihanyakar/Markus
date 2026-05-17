using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Markus.Models;
using Markus.Services;

namespace Markus.ViewModels;

internal sealed partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSourceOnly))]
    [NotifyPropertyChangedFor(nameof(IsPreviewOnly))]
    [NotifyPropertyChangedFor(nameof(IsSplitVerticalActive))]
    [NotifyPropertyChangedFor(nameof(IsSplitHorizontalActive))]
    [NotifyPropertyChangedFor(nameof(IsDetached))]
    private ViewMode _currentViewMode;

    [ObservableProperty]
    private bool _isOutlineVisible;

    [ObservableProperty]
    private string _statusText = "No file open";

    [ObservableProperty]
    private string _documentTitle = "Markus";

    [ObservableProperty]
    private string _sourceText =
        "# Welcome to Markus\n\n"
        + "This is a **placeholder**. Open a `.md` file to see it rendered.\n\n"
        + "- Live reload (coming soon)\n"
        + "- Code highlight (coming soon)\n"
        + "- Multiple themes\n";

    public MainWindowViewModel()
        : this(ServiceLocator.Settings) { }

    public MainWindowViewModel(SettingsService settingsService)
    {
        Settings = settingsService.Load();
        _currentViewMode = Settings.DefaultViewMode;
        _isOutlineVisible = Settings.ShowOutline;
    }

    public AppSettings Settings { get; private set; }

    public bool IsSourceOnly => CurrentViewMode is ViewMode.Source;

    public bool IsPreviewOnly => CurrentViewMode is ViewMode.Preview;

    public bool IsSplitVerticalActive => CurrentViewMode is ViewMode.SplitVertical;

    public bool IsSplitHorizontalActive => CurrentViewMode is ViewMode.SplitHorizontal;

    public bool IsDetached => CurrentViewMode is ViewMode.Detached;

    public void ApplySettings(AppSettings settings)
    {
        Settings = settings;
        if (CurrentViewMode != settings.DefaultViewMode)
        {
            CurrentViewMode = settings.DefaultViewMode;
        }
    }

    [RelayCommand]
    private void SetViewMode(ViewMode mode)
    {
        CurrentViewMode = mode;
    }

    [RelayCommand]
    private void ToggleOutline()
    {
        IsOutlineVisible = !IsOutlineVisible;
    }
}
