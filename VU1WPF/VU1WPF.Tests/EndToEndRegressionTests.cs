using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KR_VU1_ConfigurationManager;
using KR_VU1_Sensors;
using LibreHardwareMonitor.Hardware;
using VU1WPF;
using Xunit;

namespace VU1WPF.Tests;

public sealed class EndToEndRegressionTests : IDisposable
{
    private readonly string _configDir;

    public EndToEndRegressionTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), "VU1WPF.E2E", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configDir);
    }

    // ─── Config round-trip ─────────────────────────────────────────────────────

    [Fact]
    public async Task ConfigRoundTrip_SensorIdentifier_SurvivesSerializeAndDeserialize()
    {
        var manager = new ClassConfigurationManager(_configDir, showLoadFailureDialog: false);
        var dial = new ClassDialGUI
        {
            UID = "e2e-dial-1",
            SensorIdentifier = "/amdcpu/0/temperature/2",
            ScaleMin = 20f,
            ScaleMax = 90f,
            Thresholds = new List<ClassDialThreshold>()
        };

        manager.UpdateDialConfig(dial, saveAfter: true);
        await manager.FlushPendingConfigSaveAsync();

        var reloaded = new ClassConfigurationManager(_configDir, showLoadFailureDialog: false);

        Assert.Equal("/amdcpu/0/temperature/2", reloaded.GetDialMetric("e2e-dial-1"));
        Assert.Equal(20f, reloaded.GetDialMin("e2e-dial-1"));
        Assert.Equal(90f, reloaded.GetDialMax("e2e-dial-1"));
    }

    [Fact]
    public async Task ConfigRoundTrip_ScalingBounds_SurviveSaveAndReload()
    {
        var manager = new ClassConfigurationManager(_configDir, showLoadFailureDialog: false);
        var dial = new ClassDialGUI
        {
            UID = "e2e-dial-2",
            SensorIdentifier = "/gpu-nvidia/0/temperature/0",
            ScaleMin = 30f,
            ScaleMax = 95f,
            Thresholds = new List<ClassDialThreshold>
            {
                new() { Threshold = 60, BacklightRed = 255, BacklightGreen = 0, BacklightBlue = 0 }
            }
        };

        manager.UpdateDialConfig(dial, saveAfter: true);
        await manager.FlushPendingConfigSaveAsync();

        var reloaded = new ClassConfigurationManager(_configDir, showLoadFailureDialog: false);

        Assert.Equal(30f, reloaded.GetDialMin("e2e-dial-2"));
        Assert.Equal(95f, reloaded.GetDialMax("e2e-dial-2"));

        List<ClassDialThreshold> thresholds = reloaded.GetDialThresholds("e2e-dial-2");
        Assert.Single(thresholds);
        Assert.Equal(60, thresholds[0].Threshold);
        Assert.Equal(255, thresholds[0].BacklightRed);
    }

    // ─── Identifier fallback regression ───────────────────────────────────────

    [Fact]
    public void IdentifierFallback_ExactMatchIsPreferred_OverFallback()
    {
        string[] available =
        {
            "/amdcpu/0/temperature/2",
            "/amdcpu/0/temperature/3"
        };

        string? resolved = ClassVUSensors.VU1_SensorManager.ResolveIdentifierCandidate(
            available, "/amdcpu/0/temperature/2");

        Assert.Equal("/amdcpu/0/temperature/2", resolved);
    }

    [Fact]
    public void IdentifierFallback_RecoversWhenIndexChanges()
    {
        string[] available =
        {
            "/amdcpu/0/temperature/0",
            "/amdcpu/0/temperature/1"
        };

        string? resolved = ClassVUSensors.VU1_SensorManager.ResolveIdentifierCandidate(
            available, "/amdcpu/0/temperature/9");

        Assert.Equal("/amdcpu/0/temperature/1", resolved);
    }

    [Fact]
    public void IdentifierFallback_ReturnsNull_WhenNoSensorsAvailable()
    {
        string? resolved = ClassVUSensors.VU1_SensorManager.ResolveIdentifierCandidate(
            Array.Empty<string>(), "/amdcpu/0/temperature/2");

        Assert.Null(resolved);
    }

    // ─── Sensor null / unavailable does not produce numeric zero ──────────────

    [Fact]
    public void SensorReading_NullSensor_IsUnavailableNotZero()
    {
        SensorReadingResult result = MetricPollingService.ReadSensorValue(null);

        Assert.Equal(SensorReadingStatus.Unavailable, result.Status);
        Assert.False(result.Value.HasValue);
    }

    [Fact]
    public void SensorReading_NullValue_IsUnavailableNotZero()
    {
        var proxy = TestSensorProxy(null);

        SensorReadingResult result = MetricPollingService.ReadSensorValue(proxy);

        Assert.Equal(SensorReadingStatus.Unavailable, result.Status);
        Assert.False(result.Value.HasValue);
    }

    [Fact]
    public void SensorReading_NaNValue_IsInvalid()
    {
        var proxy = TestSensorProxy(float.NaN);

        SensorReadingResult result = MetricPollingService.ReadSensorValue(proxy);

        Assert.Equal(SensorReadingStatus.Invalid, result.Status);
        Assert.False(result.Value.HasValue);
    }

    // ─── Scaling validation regression ────────────────────────────────────────

    [Fact]
    public void ScalingValidation_EqualBoundsAreRejected_NotSilentlyAccepted()
    {
        bool valid = DialComputationEngine.AreScalingBoundsValid(100f, 100f, out string reason);

        Assert.False(valid);
        Assert.False(String.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void ScalingValidation_ValidRange_PassesAndProducesCorrectPercent()
    {
        bool valid = DialComputationEngine.AreScalingBoundsValid(20f, 100f, out _);
        int percent = DialComputationEngine.ComputeDialValuePercent(20f, 100f, 60f);

        Assert.True(valid);
        Assert.Equal(50, percent);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        try { Directory.Delete(_configDir, recursive: true); } catch { }
    }

    private static ISensor TestSensorProxy(float? valueToReturn)
    {
        var proxy = (SensorProxy)(object)DispatchProxy.Create<ISensor, SensorProxy>();
        proxy.ReturnValue = valueToReturn;
        return (ISensor)(object)proxy;
    }

    private class SensorProxy : DispatchProxy
    {
        public float? ReturnValue { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_Value") return ReturnValue;
            if (targetMethod?.Name == "get_Hardware") return (IHardware)(object)HardwareStub();
            if (targetMethod?.Name == "get_Identifier") return new Identifier("/amdcpu/0/temperature/2");
            if (targetMethod?.ReturnType == typeof(void)) return null;
            if (targetMethod?.ReturnType == typeof(string)) return string.Empty;
            if (targetMethod?.ReturnType.IsValueType == true) return Activator.CreateInstance(targetMethod.ReturnType);
            return null;
        }

        private static IHardware HardwareStub()
        {
            var hw = (HardwareProxy)(object)DispatchProxy.Create<IHardware, HardwareProxy>();
            return (IHardware)(object)hw;
        }
    }

    private class HardwareProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.ReturnType == typeof(void)) return null;
            if (targetMethod?.ReturnType == typeof(string)) return string.Empty;
            if (targetMethod?.Name == "get_Parent") return null;
            if (targetMethod?.ReturnType.IsValueType == true) return Activator.CreateInstance(targetMethod.ReturnType);
            return null;
        }
    }
}
