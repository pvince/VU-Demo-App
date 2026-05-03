using System;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace VU1WPF.Tests.UI;

[Collection(FlaUiAppCollection.Name)]
public sealed class ModalSmokeTests
{
    private readonly FlaUiAppFixture _fixture;

    public ModalSmokeTests(FlaUiAppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "UI")]
    public void AboutDialog_Opens_AndCloses()
    {
        var aboutButton = UiElementAssertions
            .RequireElementByAutomationIdOnly(_fixture.MainWindow, SelectorConstants.MainWindowAboutButton)
            .AsButton();

        aboutButton.Invoke();

        Window aboutWindow = UiElementAssertions.RequireTopLevelWindowByTitle(
            _fixture,
            "VU Dials - About The Demo App",
            timeout: TimeSpan.FromSeconds(8));

        var closeButton = UiElementAssertions
            .RequireElementByAutomationIdOnly(aboutWindow, SelectorConstants.AboutDialogCloseButton)
            .AsButton();

        closeButton.Invoke();
    }

    [Fact]
    [Trait("Category", "UI")]
    public void ThresholdDialog_SetRulesButton_IsInvokable_WhenDialSelectionExists()
    {
        var dialList = UiElementAssertions
            .RequireElementByAutomationIdOnly(_fixture.MainWindow, SelectorConstants.MainWindowDialList)
            .AsListBox();

        if (dialList.Items.Length == 0)
        {
            return;
        }

        dialList.Items[0].Select();

        var setRulesButton = UiElementAssertions
            .RequireElementByAutomationIdOnly(_fixture.MainWindow, SelectorConstants.MainWindowSetRulesButton)
            .AsButton();

        UiElementAssertions.WaitUntilEnabled(setRulesButton);
        setRulesButton.Invoke();

        // Dialog opening depends on selected dial UID/runtime state.
        // Smoke-level check only verifies the control interaction path remains invokable.
    }
}
