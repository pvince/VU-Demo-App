using System;
using System.Linq;
using FlaUI.Core.Definitions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Xunit;

namespace VU1WPF.Tests.UI;

internal static class SelectorConstants
{
    public const string MainWindowCloseButton = "MainWindow_Close_Button";
    public const string MainWindowMinimizeButton = "MainWindow_Minimize_Button";
    public const string MainWindowTrayButton = "MainWindow_Tray_Button";
    public const string MainWindowRefreshDialsButton = "MainWindow_RefreshDials_Button";
    public const string MainWindowToggleDialUpdateButton = "MainWindow_ToggleDialUpdate_Button";
    public const string MainWindowServerHostTextBox = "MainWindow_ServerHost_TextBox";
    public const string MainWindowServerPortTextBox = "MainWindow_ServerPort_TextBox";
    public const string MainWindowReconnectButton = "MainWindow_Reconnect_Button";
    public const string MainWindowConnectionStatusLabel = "MainWindow_ConnectionStatus_Label";
    public const string MainWindowSaveDialConfigButton = "MainWindow_SaveDialConfig_Button";
    public const string MainWindowSetImageButton = "MainWindow_SetImage_Button";
    public const string MainWindowSetColorButton = "MainWindow_SetColor_Button";
    public const string MainWindowAboutButton = "MainWindow_About_Button";
    public const string MainWindowSetRulesButton = "MainWindow_SetRules_Button";
    public const string MainWindowDialList = "MainWindow_DialList_ListBox";
    public const string MainWindowRunOnStartupCheckBox = "MainWindow_RunOnStartup_CheckBox";
    public const string MainWindowDialNameTextBox = "MainWindow_DialName_TextBox";
    public const string MainWindowCurrentMetricLabel = "MainWindow_CurrentMetric_Label";
    public const string MainWindowCurrentPercentLabel = "MainWindow_CurrentPercent_Label";
    public const string MainWindowCurrentValueLabel = "MainWindow_CurrentValue_Label";
    public const string MainWindowMetricStatusText = "MainWindow_MetricStatus_Text";
    public const string MainWindowMetricCategoryComboBox = "MainWindow_MetricCategory_ComboBox";
    public const string MainWindowSelectedMetricComboBox = "MainWindow_SelectedMetric_ComboBox";
    public const string MainWindowMinValueTextBox = "MainWindow_MinValue_TextBox";
    public const string MainWindowMaxValueTextBox = "MainWindow_MaxValue_TextBox";
    public const string MainWindowBrandingLabel = "MainWindow_Branding_Label";
    public const string AboutDialogCloseButton = "AboutDialog_Close_Button";
    public const string ThresholdRulesComboBox = "ThresholdDialog_Rules_ComboBox";
}

internal static class UiElementAssertions
{
    public static AutomationElement RequireElementByAutomationIdOnly(Window window, string automationId, TimeSpan? timeout = null)
    {
        var result = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            timeout: timeout ?? TimeSpan.FromSeconds(20),
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
            timeout: timeout ?? TimeSpan.FromSeconds(20),
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
            timeout: timeout ?? TimeSpan.FromSeconds(20),
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
            timeout: timeout ?? TimeSpan.FromSeconds(20),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false,
            ignoreException: true);

        Assert.True(result.Success, $"Expected at least {minCount} controls of type '{controlType}'.");
    }

    public static void WaitUntilEnabled(AutomationElement element, TimeSpan? timeout = null)
    {
        var result = Retry.WhileFalse(
            () => element.IsEnabled,
            timeout: timeout ?? TimeSpan.FromSeconds(20),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false,
            ignoreException: true);

        Assert.True(result.Success, "UI element did not become enabled within timeout.");
    }

    public static void RequireElementVisibleWithinWindow(Window window, AutomationElement element, string identifier)
    {
        var windowRect = window.BoundingRectangle;
        var elementRect = element.BoundingRectangle;

        Assert.True(elementRect.Width > 0 && elementRect.Height > 0, $"Element '{identifier}' has empty bounds.");
        Assert.True(
            elementRect.Left >= windowRect.Left
            && elementRect.Top >= windowRect.Top
            && elementRect.Right <= windowRect.Right
            && elementRect.Bottom <= windowRect.Bottom,
            $"Element '{identifier}' is not fully visible within the main window.");
    }

    public static void RequireElementsDoNotOverlap(AutomationElement first, string firstName, AutomationElement second, string secondName)
    {
        var firstRect = first.BoundingRectangle;
        var secondRect = second.BoundingRectangle;

        Assert.True(firstRect.Width > 0 && firstRect.Height > 0, $"Element '{firstName}' has empty bounds.");
        Assert.True(secondRect.Width > 0 && secondRect.Height > 0, $"Element '{secondName}' has empty bounds.");

        bool overlaps = firstRect.Left < secondRect.Right
            && firstRect.Right > secondRect.Left
            && firstRect.Top < secondRect.Bottom
            && firstRect.Bottom > secondRect.Top;

        Assert.False(overlaps, $"Elements '{firstName}' and '{secondName}' overlap.");
    }

    public static Window RequireTopLevelWindowByTitle(FlaUiAppFixture fixture, string title, TimeSpan? timeout = null)
    {
        var result = Retry.WhileNull(
            () => fixture.App.GetAllTopLevelWindows(fixture.Automation).FirstOrDefault(w => w.Title == title),
            timeout: timeout ?? TimeSpan.FromSeconds(20),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false,
            ignoreException: true);

        Assert.True(result.Success && result.Result is not null, $"Missing top-level window '{title}'.");
        return result.Result!;
    }
}
