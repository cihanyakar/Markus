using Markus.Models;
using Markus.ViewModels;

namespace Markus.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void DefaultViewMode_FollowsSettings()
    {
        var sut = new MainWindowViewModel();

        sut.CurrentViewMode.ShouldBe(sut.Settings.DefaultViewMode);
    }

    [Fact]
    public void IsSourceOnly_TrueOnlyForSourceMode()
    {
        var sut = new MainWindowViewModel { CurrentViewMode = ViewMode.Source };

        sut.IsSourceOnly.ShouldBeTrue();
        sut.IsPreviewOnly.ShouldBeFalse();
        sut.IsSplitVerticalActive.ShouldBeFalse();
    }

    [Fact]
    public void IsSplitVerticalActive_TrueForVerticalSplit()
    {
        var sut = new MainWindowViewModel { CurrentViewMode = ViewMode.SplitVertical };

        sut.IsSplitVerticalActive.ShouldBeTrue();
        sut.IsSourceOnly.ShouldBeFalse();
        sut.IsPreviewOnly.ShouldBeFalse();
    }

    [Fact]
    public void ToggleOutline_FlipsVisibility()
    {
        var sut = new MainWindowViewModel();
        var before = sut.IsOutlineVisible;

        sut.ToggleOutlineCommand.Execute(null);

        sut.IsOutlineVisible.ShouldBe(!before);
    }
}
