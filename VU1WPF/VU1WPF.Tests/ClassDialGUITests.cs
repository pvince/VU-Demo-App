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
                return new Identifier("/amdcpu/0/temperature/2");
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