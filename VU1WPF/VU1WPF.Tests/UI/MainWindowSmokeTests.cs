using System;
using FlaUI.Core.AutomationElements;
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
    public void Startup_FindsCoreControlsByAutomationId()
    {
        UiElementAssertions.RequireElementByAutomationIdOnly(_fixture.MainWindow, SelectorConstants.MainWindowDialList);
        UiElementAssertions.RequireElementByAutomationIdOnly(_fixture.MainWindow, SelectorConstants.MainWindowServerHostTextBox);
        UiElementAssertions.RequireElementByAutomationIdOnly(_fixture.MainWindow, SelectorConstants.MainWindowServerPortTextBox);
        UiElementAssertions.RequireElementByAutomationIdOnly(_fixture.MainWindow, SelectorConstants.MainWindowReconnectButton);
        UiElementAssertions.RequireElementByAutomationIdOnly(_fixture.MainWindow, SelectorConstants.MainWindowAboutButton);
    }

    [Fact]
    [Trait("Category", "UI")]
    public void Startup_AllMainButtons_AreVisibleWithinWindow()
    {
        string[] buttonIds =
        {
            SelectorConstants.MainWindowCloseButton,
            SelectorConstants.MainWindowMinimizeButton,
            SelectorConstants.MainWindowTrayButton,
            SelectorConstants.MainWindowRefreshDialsButton,
            SelectorConstants.MainWindowToggleDialUpdateButton,
            SelectorConstants.MainWindowReconnectButton,
            SelectorConstants.MainWindowSaveDialConfigButton,
            SelectorConstants.MainWindowSetImageButton,
            SelectorConstants.MainWindowSetRulesButton,
            SelectorConstants.MainWindowSetColorButton,
            SelectorConstants.MainWindowAboutButton,
        };

        foreach (string buttonId in buttonIds)
        {
            var button = UiElementAssertions.RequireElementByAutomationIdOnly(_fixture.MainWindow, buttonId).AsButton();
            UiElementAssertions.RequireElementVisibleWithinWindow(_fixture.MainWindow, button, buttonId);
        }
    }

    [Fact]
    [Trait("Category", "UI")]
    public void Startup_AllExpectedVisibleElements_AreFullyVisibleWithinWindow()
    {
        string[] visibleElementIds =
        {
            SelectorConstants.MainWindowDialList,
            SelectorConstants.MainWindowRunOnStartupCheckBox,
            SelectorConstants.MainWindowServerHostTextBox,
            SelectorConstants.MainWindowServerPortTextBox,
            SelectorConstants.MainWindowReconnectButton,
            SelectorConstants.MainWindowConnectionStatusLabel,
            SelectorConstants.MainWindowDialNameTextBox,
            SelectorConstants.MainWindowCurrentMetricLabel,
            SelectorConstants.MainWindowCurrentPercentLabel,
            SelectorConstants.MainWindowCurrentValueLabel,
            SelectorConstants.MainWindowMetricStatusText,
            SelectorConstants.MainWindowMetricCategoryComboBox,
            SelectorConstants.MainWindowSelectedMetricComboBox,
            SelectorConstants.MainWindowMinValueTextBox,
            SelectorConstants.MainWindowMaxValueTextBox,
            SelectorConstants.MainWindowBrandingLabel,
        };

        foreach (string elementId in visibleElementIds)
        {
            var element = UiElementAssertions.RequireElementByAutomationIdOnly(_fixture.MainWindow, elementId);
            UiElementAssertions.RequireElementVisibleWithinWindow(_fixture.MainWindow, element, elementId);
        }
    }

    [Fact]
    [Trait("Category", "UI")]
    public void Startup_FooterTexts_DoNotOverlap()
    {
        var connectionStatus = UiElementAssertions.RequireElementByAutomationIdOnly(
            _fixture.MainWindow,
            SelectorConstants.MainWindowConnectionStatusLabel);
        var brandingLabel = UiElementAssertions.RequireElementByAutomationIdOnly(
            _fixture.MainWindow,
            SelectorConstants.MainWindowBrandingLabel);

        UiElementAssertions.RequireElementVisibleWithinWindow(
            _fixture.MainWindow,
            connectionStatus,
            SelectorConstants.MainWindowConnectionStatusLabel);
        UiElementAssertions.RequireElementVisibleWithinWindow(
            _fixture.MainWindow,
            brandingLabel,
            SelectorConstants.MainWindowBrandingLabel);
        UiElementAssertions.RequireElementsDoNotOverlap(
            connectionStatus,
            SelectorConstants.MainWindowConnectionStatusLabel,
            brandingLabel,
            SelectorConstants.MainWindowBrandingLabel);
    }

    [Fact]
    [Trait("Category", "UI")]
    public void Startup_ServerFieldsAcceptInput()
    {
        var hostTextBox = UiElementAssertions
            .RequireElementByAutomationIdOnly(_fixture.MainWindow, SelectorConstants.MainWindowServerHostTextBox)
            .AsTextBox();
        var portTextBox = UiElementAssertions
            .RequireElementByAutomationIdOnly(_fixture.MainWindow, SelectorConstants.MainWindowServerPortTextBox)
            .AsTextBox();

        hostTextBox.Text = "localhost";
        portTextBox.Text = "5340";

        Assert.Equal("localhost", hostTextBox.Text);
        Assert.Equal("5340", portTextBox.Text);
    }

    [Fact]
    [Trait("Category", "UI")]
    public void Startup_MainWindowProcessIsRunning()
    {
        Assert.False(_fixture.App.HasExited);
    }
}
