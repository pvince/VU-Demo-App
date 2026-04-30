using System.Reflection;
using LibreHardwareMonitor.Hardware;
using VU1WPF;

namespace VU1WPF.Tests;

public sealed class MetricPollingServiceTests
{
    [Fact]
    public void ReadSensorValue_ReturnsUnavailable_WhenSensorIsNull()
    {
        SensorReadingResult result = MetricPollingService.ReadSensorValue(null);

        Assert.Equal(SensorReadingStatus.Unavailable, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ReadSensorValue_ReturnsInvalid_WhenValueIsNaN()
    {
        ISensor sensor = CreateSensorProxy(float.NaN);

        SensorReadingResult result = MetricPollingService.ReadSensorValue(sensor);

        Assert.Equal(SensorReadingStatus.Invalid, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ReadSensorValue_ReturnsAvailable_WhenValueIsFinite()
    {
        ISensor sensor = CreateSensorProxy(62.25f);

        SensorReadingResult result = MetricPollingService.ReadSensorValue(sensor);

        Assert.Equal(SensorReadingStatus.Available, result.Status);
        Assert.Equal(62.25f, result.Value);
    }

    [Fact]
    public void ReadSensorValue_InvokesHardwareUpdate()
    {
        var hardware = (HardwareProxy)(object)DispatchProxy.Create<IHardware, HardwareProxy>();
        hardware.ThrowOnUpdate = false;

        ISensor sensor = CreateSensorProxy(43.1f, hardware);

        _ = MetricPollingService.ReadSensorValue(sensor);

        Assert.Equal(1, hardware.UpdateCallCount);
    }

    [Fact]
    public void ReadSensorValue_ReturnsInvalid_WhenUpdateThrows()
    {
        var hardware = (HardwareProxy)(object)DispatchProxy.Create<IHardware, HardwareProxy>();
        hardware.ThrowOnUpdate = true;
        ISensor sensor = CreateSensorProxy(55f, hardware);

        SensorReadingResult result = MetricPollingService.ReadSensorValue(sensor);

        Assert.Equal(SensorReadingStatus.Invalid, result.Status);
        Assert.Null(result.Value);
    }

    private static ISensor CreateSensorProxy(float? value, HardwareProxy? hardware = null)
    {
        var sensorProxy = (SensorProxy)(object)DispatchProxy.Create<ISensor, SensorProxy>();
        sensorProxy.ValueToReturn = value;
        sensorProxy.HardwareProxy = hardware ?? (HardwareProxy)(object)DispatchProxy.Create<IHardware, HardwareProxy>();
        return (ISensor)(object)sensorProxy;
    }

    private class SensorProxy : DispatchProxy
    {
        public float? ValueToReturn { get; set; }
        public HardwareProxy HardwareProxy { get; set; } = (HardwareProxy)(object)DispatchProxy.Create<IHardware, HardwareProxy>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_Value")
            {
                return ValueToReturn;
            }

            if (targetMethod?.Name == "get_Hardware")
            {
                return (IHardware)(object)HardwareProxy;
            }

            if (targetMethod?.Name == "get_Identifier")
            {
                return new Identifier("/amdcpu/0/temperature/2");
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

    private class HardwareProxy : DispatchProxy
    {
        public int UpdateCallCount { get; private set; }
        public bool ThrowOnUpdate { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Update")
            {
                UpdateCallCount++;
                if (ThrowOnUpdate)
                {
                    throw new InvalidOperationException("update-failed");
                }

                return null;
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