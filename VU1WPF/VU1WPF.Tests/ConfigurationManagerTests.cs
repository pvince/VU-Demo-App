using System;
using System.Collections.Generic;
using System.IO;
using KR_VU1_ConfigurationManager;
using VU1WPF;
using Xunit;

namespace VU1WPF.Tests;

public sealed class ConfigurationManagerTests : IDisposable
{
    private readonly string _configDirectory;

    public ConfigurationManagerTests()
    {
        _configDirectory = Path.Combine(Path.GetTempPath(), "VU1WPF.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configDirectory);
    }

    [Fact]
    public void Constructor_UsesDefaultServerValues_WhenConfigContainsBlankHostOrInvalidPort()
    {
        File.WriteAllText(
            Path.Combine(_configDirectory, "vu1demo_config.yaml"),
            "master_key: abc\n" +
            "dial_update_period: 0.5\n" +
            "server_host: \"\"\n" +
            "server_port: 0\n" +
            "dial_metrics: []\n");

        var manager = new ClassConfigurationManager(_configDirectory, showLoadFailureDialog: false);

        Assert.Equal("localhost", manager.GetServerHost());
        Assert.Equal(5340, manager.GetServerPort());
    }

    [Fact]
    public void UpdateDialConfig_StoresMetricScalingAndThresholds_ForDialWithoutAttachedSensor()
    {
        var manager = new ClassConfigurationManager(_configDirectory, showLoadFailureDialog: false);
        var dial = new ClassDialGUI
        {
            UID = "dial-1",
            Metric = "Temperature",
            ScaleMin = 10f,
            ScaleMax = 90f,
            Thresholds = new List<ClassDialThreshold>
            {
                new()
                {
                    Threshold = 50,
                    BacklightRed = 12,
                    BacklightGreen = 34,
                    BacklightBlue = 56,
                }
            }
        };

        bool updated = manager.UpdateDialConfig(dial, saveAfter: false);

        Assert.True(updated);
        Assert.Equal("Temperature", manager.GetDialMetric("dial-1"));
        Assert.Equal(10f, manager.GetDialMin("dial-1"));
        Assert.Equal(90f, manager.GetDialMax("dial-1"));

        List<ClassDialThreshold> thresholds = manager.GetDialThresholds("dial-1");
        Assert.Single(thresholds);
        Assert.Equal(50, thresholds[0].Threshold);
        Assert.Equal(12, thresholds[0].BacklightRed);
        Assert.Equal(34, thresholds[0].BacklightGreen);
        Assert.Equal(56, thresholds[0].BacklightBlue);
    }

    [Fact]
    public void UpdateDialConfig_PreservesStoredSensorIdentifier_WhenSensorIsUnavailable()
    {
        var manager = new ClassConfigurationManager(_configDirectory, showLoadFailureDialog: false);
        var dial = new ClassDialGUI
        {
            UID = "dial-2",
            SensorIdentifier = "/amdcpu/0/temperature/2",
            ScaleMin = 5f,
            ScaleMax = 95f,
            Thresholds = new List<ClassDialThreshold>
            {
                new()
                {
                    Threshold = 60,
                    BacklightRed = 1,
                    BacklightGreen = 2,
                    BacklightBlue = 3,
                }
            }
        };

        bool created = manager.UpdateDialConfig(dial, saveAfter: false);

        Assert.True(created);
        Assert.Equal("/amdcpu/0/temperature/2", manager.GetDialMetric("dial-2"));

        dial.ScaleMin = 15f;
        dial.ScaleMax = 85f;
        dial.Thresholds = new List<ClassDialThreshold>
        {
            new()
            {
                Threshold = 75,
                BacklightRed = 4,
                BacklightGreen = 5,
                BacklightBlue = 6,
            }
        };

        bool updated = manager.UpdateDialConfig(dial, saveAfter: false);

        Assert.True(updated);
        Assert.Equal("/amdcpu/0/temperature/2", manager.GetDialMetric("dial-2"));
        Assert.Equal(15f, manager.GetDialMin("dial-2"));
        Assert.Equal(85f, manager.GetDialMax("dial-2"));

        List<ClassDialThreshold> thresholds = manager.GetDialThresholds("dial-2");
        Assert.Single(thresholds);
        Assert.Equal(75, thresholds[0].Threshold);
        Assert.Equal(4, thresholds[0].BacklightRed);
        Assert.Equal(5, thresholds[0].BacklightGreen);
        Assert.Equal(6, thresholds[0].BacklightBlue);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_configDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(_configDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}