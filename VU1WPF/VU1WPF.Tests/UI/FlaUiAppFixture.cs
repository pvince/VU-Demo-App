using System;
using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace VU1WPF.Tests.UI;

public sealed class FlaUiAppFixture : IDisposable
{
    public Application App { get; }

    public UIA3Automation Automation { get; }

    public Window MainWindow { get; }

    public string AppBinaryPath { get; }

    public FlaUiAppFixture()
    {
        AppBinaryPath = ResolveAppBinaryPath();

        App = Application.Launch(AppBinaryPath);
        Automation = new UIA3Automation();

        var mainWindowResult = Retry.WhileNull(
            () => App.GetMainWindow(Automation),
            timeout: TimeSpan.FromSeconds(20),
            interval: TimeSpan.FromMilliseconds(150),
            throwOnTimeout: false,
            ignoreException: true);

        if (!mainWindowResult.Success || mainWindowResult.Result is null)
        {
            Dispose();
            throw new InvalidOperationException("Failed to find main window for UI smoke tests.");
        }

        MainWindow = mainWindowResult.Result;
    }

    public void Dispose()
    {
        try
        {
            if (MainWindow?.IsAvailable == true)
            {
                MainWindow.Close();
            }
        }
        catch
        {
        }

        try
        {
            if (!App.HasExited)
            {
                App.Close();
            }
        }
        catch
        {
        }

        try
        {
            if (!App.HasExited)
            {
                App.Kill();
            }
        }
        catch
        {
        }

        Automation.Dispose();
    }

    private static string ResolveAppBinaryPath()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("VU1WPF_UI_APP_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (!File.Exists(configuredPath))
            {
                throw new FileNotFoundException($"VU1WPF_UI_APP_PATH points to a missing executable: {configuredPath}");
            }

            return configuredPath;
        }

        string[] relativeCandidates =
        {
            Path.Combine("VU1WPF", "bin", "UIInvestigation", "net8.0-windows", "VU1-Demo-App.exe"),
            Path.Combine("VU1WPF", "bin", "Debug", "net8.0-windows", "VU1-Demo-App.exe"),
            Path.Combine("VU1WPF", "bin", "Release", "net8.0-windows", "VU1-Demo-App.exe"),
        };

        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            foreach (string candidate in relativeCandidates)
            {
                string fullPath = Path.Combine(current.FullName, candidate);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            current = current.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate VU1-Demo-App.exe. Build UIInvestigation from VU1WPF or set VU1WPF_UI_APP_PATH.");
    }
}
