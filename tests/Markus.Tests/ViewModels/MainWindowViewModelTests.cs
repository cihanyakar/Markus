using Markus.ViewModels;

namespace Markus.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Greeting_HasDefaultValue()
    {
        var sut = new MainWindowViewModel();

        sut.Greeting.ShouldBe("Welcome to Avalonia!");
    }
}
