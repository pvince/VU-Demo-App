using FlaUI.Core.AutomationElements;
using Xunit;

namespace VU1WPF.Tests.UI;

[Collection(FlaUiAppCollection.Name)]
public sealed class ReconnectSmokeTests : IClassFixture<FlaUiAppFixture>
{
    private readonly FlaUiAppFixture _fixture;

    public ReconnectSmokeTests(FlaUiAppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "UI")]
    public void ReconnectButton_IsEnabled_AndInvokable()
    {
        var reconnectButton = UiElementAssertions
            .RequireElementByAnyIdentifier(_fixture.MainWindow, SelectorConstants.MainWindowReconnectButton, "Reconnect")
            .AsButton();

        UiElementAssertions.WaitUntilEnabled(reconnectButton);
        reconnectButton.Invoke();

        UiElementAssertions.RequireElementByAutomationIdOnly(_fixture.MainWindow, SelectorConstants.MainWindowConnectionStatusLabel);
    }
}
