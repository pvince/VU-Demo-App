using System;
using System.Linq;
using Xunit;

namespace VU1WPF.Tests.UI;

[Collection(FlaUiAppCollection.Name)]
public sealed class MainWindowSmokeTests
{
    private readonly FlaUiAppFixture _fixture;

    public MainWindowSmokeTests(FlaUiAppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "UI")]
    public void Startup_ShowsMainWindowWithExpectedTitle()
    {
        Assert.Equal("VU1 - Companion App", _fixture.MainWindow.Title);
    }

    [Fact]
    [Trait("Category", "UI")]
    public void Startup_MainWindowHasAutomationDescendants()
    {
        int descendantCount = _fixture.MainWindow.FindAllDescendants().Length;

        Assert.True(descendantCount > 0, "Expected main window automation tree to contain descendants.");
    }

    [Fact]
    [Trait("Category", "UI")]
    public void Startup_AppProcessIsRunning_AndHasTopLevelWindow()
    {
        Assert.False(_fixture.App.HasExited);

        var topLevelWindows = _fixture.App.GetAllTopLevelWindows(_fixture.Automation);

        Assert.True(topLevelWindows.Any(), "Expected at least one top-level window for the launched app.");
    }
}
