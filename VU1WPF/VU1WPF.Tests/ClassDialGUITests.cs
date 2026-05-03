using VU1WPF;
using LibreHardwareMonitor.Hardware;
using System.Reflection;

namespace VU1WPF.Tests;

public sealed class ClassDialGUITests
{
    [Fact]
    public void SensorStatus_IsNotConfigured_WhenNoIdentifierOrSensor()
    {
        var dial = new ClassDialGUI();

        Assert.Equal(SensorBindingStatus.NotConfigured, dial.SensorStatus);
        Assert.False(dial.HasUnavailableSensorBinding);
    }

    [Fact]
    public void SensorStatus_IsUnavailable_WhenSensorIsNullButIdentifierIsSet()
    {
        var dial = new ClassDialGUI
        {
            SensorIdentifier = "/amdcpu/0/temperature/2"
        };

        Assert.Equal(SensorBindingStatus.Unavailable, dial.SensorStatus);
        Assert.True(dial.HasUnavailableSensorBinding);
    }

    [Fact]
    public void SensorStatus_IsAvailable_WhenSensorIsAttached()
    {
        var dial = new ClassDialGUI
        {
            SensorIdentifier = "/amdcpu/0/temperature/2",
            Sensor = CreateSensorProxy()
        };

        Assert.Equal(SensorBindingStatus.Available, dial.SensorStatus);
        Assert.False(dial.HasUnavailableSensorBinding);
    }

    [Fact]
    public void ConfiguredSensorIdentifier_UsesAttachedSensorIdentifier_WhenSensorIsPresent()
    {
        ISensor sensor = CreateSensorProxy();
        var dial = new ClassDialGUI
        {
            Metric = "Temperature",
            SensorIdentifier = "/saved/identifier",
            Sensor = sensor
        };

        Assert.Equal(sensor.Identifier.ToString(), dial.ConfiguredSensorIdentifier);
        Assert.True(dial.HasConfiguredSensorBinding);
    }

    [Fact]
    public void ConfiguredSensorIdentifier_UsesStoredIdentifier_WhenSensorIsMissing()
    {
        var dial = new ClassDialGUI
        {
            Metric = "Temperature",
            SensorIdentifier = "/saved/identifier"
        };

        Assert.Equal("/saved/identifier", dial.ConfiguredSensorIdentifier);
        Assert.True(dial.HasConfiguredSensorBinding);
    }

    [Fact]
    public void ConfiguredSensorIdentifier_FallsBackToMetric_WhenNoSensorIdentifierExists()
    {
        var dial = new ClassDialGUI
        {
            Metric = "Temperature"
        };

        Assert.Equal("Temperature", dial.ConfiguredSensorIdentifier);
        Assert.True(dial.HasConfiguredSensorBinding);
    }

    [Fact]
    public void BacklightPresets_ExposeExpectedNamedColors()
    {
        Assert.Contains(BacklightPresets.AvailablePresets, preset =>
            preset.Name == "Off" &&
            preset.Red == 0 &&
            preset.Green == 0 &&
            preset.Blue == 0);

        Assert.Contains(BacklightPresets.AvailablePresets, preset =>
            preset.Name == "White" &&
            preset.Red == 100 &&
            preset.Green == 100 &&
            preset.Blue == 100);
    }

    private static Identifier CreateIdentifier(string identifier)
    {
        ConstructorInfo ctor = typeof(Identifier).GetConstructor(new[] { typeof(Identifier), typeof(string[]) })!;
        string[] parts = identifier.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (Identifier)ctor.Invoke(new object?[] { null, parts });
    }

    private static ISensor CreateSensorProxy()
    {
        return DispatchProxy.Create<ISensor, SensorProxy>();
    }

    private class SensorProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_Identifier")
            {
                return CreateIdentifier("/amdcpu/0/temperature/2");
            }

            if (targetMethod?.Name == "get_Name")
            {
                return "Fake sensor";
            }

            if (targetMethod?.ReturnType == typeof(void))
            {
                return null;
            }

            if (targetMethod?.ReturnType == typeof(string))
            {
                return string.Empty;
            }

            if (targetMethod?.ReturnType.IsValueType == true)
            {
                return Activator.CreateInstance(targetMethod.ReturnType);
            }

            return null;
        }
    }
}