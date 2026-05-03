using System;
using System.Linq;
using FlaUI.Core.Definitions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Xunit;

namespace VU1WPF.Tests.UI;

internal static class SelectorConstants
{
    public const string MainWindowServerHostTextBox = "MainWindow_ServerHost_TextBox";
    public const string MainWindowServerPortTextBox = "MainWindow_ServerPort_TextBox";
    public const string MainWindowReconnectButton = "MainWindow_Reconnect_Button";
    public const string MainWindowConnectionStatusLabel = "MainWindow_ConnectionStatus_Label";
    public const string MainWindowAboutButton = "MainWindow_About_Button";
    public const string MainWindowSetRulesButton = "MainWindow_SetRules_Button";
    public const string MainWindowDialList = "MainWindow_DialList_ListBox";
    public const string AboutDialogCloseButton = "AboutDialog_Close_Button";
    public const string ThresholdRulesComboBox = "ThresholdDialog_Rules_ComboBox";
}

internal static class UiElementAssertions
{
    public static AutomationElement RequireElementByAutomationIdOnly(Window window, string automationId, TimeSpan? timeout = null)
    {
        var result = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            timeout: timeout ?? TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false,
            ignoreException: true);

        Assert.True(result.Success && result.Result is not null, $"Missing UI element with AutomationId '{automationId}'.");
        return result.Result!;
    }

    public static AutomationElement RequireElementByAutomationIdOrName(Window window, string identifier, TimeSpan? timeout = null)
    {
        var result = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId(identifier))
                ?? window.FindFirstDescendant(cf => cf.ByName(identifier)),
            timeout: timeout ?? TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false,
            ignoreException: true);

        Assert.True(result.Success && result.Result is not null, $"Missing UI element by AutomationId or Name '{identifier}'.");
        return result.Result!;
    }

    public static AutomationElement RequireElementByControlType(Window window, ControlType controlType, TimeSpan? timeout = null)
    {
        var result = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByControlType(controlType)),
            timeout: timeout ?? TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false,
            ignoreException: true);

        Assert.True(result.Success && result.Result is not null, $"Missing UI control of type '{controlType}'.");
        return result.Result!;
    }

    public static void RequireMinimumControlCountByType(Window window, ControlType controlType, int minCount, TimeSpan? timeout = null)
    {
        var result = Retry.WhileFalse(
            () => window.FindAllDescendants(cf => cf.ByControlType(controlType)).Length >= minCount,
            timeout: timeout ?? TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false,
            ignoreException: true);

        Assert.True(result.Success, $"Expected at least {minCount} controls of type '{controlType}'.");
    }

    public static void WaitUntilEnabled(AutomationElement element, TimeSpan? timeout = null)
    {
        var result = Retry.WhileFalse(
            () => element.IsEnabled,
            timeout: timeout ?? TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false,
            ignoreException: true);

        Assert.True(result.Success, "UI element did not become enabled within timeout.");
    }

    public static Window RequireTopLevelWindowByTitle(FlaUiAppFixture fixture, string title, TimeSpan? timeout = null)
    {
        var result = Retry.WhileNull(
            () => fixture.App.GetAllTopLevelWindows(fixture.Automation).FirstOrDefault(w => w.Title == title),
            timeout: timeout ?? TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false,
            ignoreException: true);

        Assert.True(result.Success && result.Result is not null, $"Missing top-level window '{title}'.");
        return result.Result!;
    }
}
