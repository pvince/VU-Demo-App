using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LibreHardwareMonitor.Hardware;
using KR_VU1_Sensors;

namespace VU1WPF.Tests;

public sealed class ClassVUSensorsTests
{
    [Fact]
    public void SensorCtor_UsesIdentifierAsDisplayName_WhenNameIsBlank()
    {
        ISensor sensor = CreateSensor("/amdcpu/0/temperature/2", SensorType.Temperature);

        var wrapped = new ClassVUSensors.VU1_Sensor(sensor);

        Assert.Equal(sensor.Identifier.ToString(), wrapped.DisplayName);
    }

    [Fact]
    public void SensorCtor_UsesProvidedDisplayName_WhenNameIsPresent()
    {
        ISensor sensor = CreateSensor("/amdcpu/0/temperature/2", SensorType.Temperature);

        var wrapped = new ClassVUSensors.VU1_Sensor(sensor, "CPU Package");

        Assert.Equal("CPU Package", wrapped.DisplayName);
    }

    [Fact]
    public void SeededManager_ReturnsAvailableAndUsedSensors()
    {
        var seededSensors = new List<ClassVUSensors.VU1_Sensor>
        {
            new(CreateSensor("/amdcpu/0/temperature/1", SensorType.Temperature)),
            new(CreateSensor("/amdcpu/0/power/0", SensorType.Power))
        };

        var manager = new ClassVUSensors.VU1_SensorManager(seededSensors, skipHardwareInitialization: true);

        Assert.Equal(2, manager.get_available_sensors().Count);
        Assert.Equal(2, manager.get_used_sensors().Count);
    }

    [Fact]
    public void FindSensorByIdentifier_ReturnsNull_WhenIdentifierIsBlank()
    {
        var manager = new ClassVUSensors.VU1_SensorManager(new List<ClassVUSensors.VU1_Sensor>(), skipHardwareInitialization: true);

        Assert.Null(manager.FindSensorByIdentifier(string.Empty));
        Assert.Null(manager.FindSensorByIdentifier("   "));
    }

    [Fact]
    public void FindSensorByIdentifier_ReturnsNull_WhenNoCandidateMatches()
    {
        var manager = new ClassVUSensors.VU1_SensorManager(
            new List<ClassVUSensors.VU1_Sensor>
            {
                new(CreateSensor("/nvidia/0/load/0", SensorType.Load))
            },
            skipHardwareInitialization: true);

        ClassVUSensors.VU1_Sensor? found = manager.FindSensorByIdentifier("/amdcpu/0/temperature/2");

        Assert.Null(found);
    }

    [Fact]
    public void FindSensorByIdentifier_ReturnsFallbackMatch_WhenExactIdentifierChanged()
    {
        ISensor sensor = CreateSensor("/amdcpu/0/temperature/3", SensorType.Temperature);
        var manager = new ClassVUSensors.VU1_SensorManager(
            new List<ClassVUSensors.VU1_Sensor>
            {
                new(sensor)
            },
            skipHardwareInitialization: true);

        ClassVUSensors.VU1_Sensor? found = manager.FindSensorByIdentifier("/amdcpu/0/temperature/9");

        Assert.NotNull(found);
        Assert.Same(sensor, found!.Sensor);
    }

    [Fact]
    public void ResolveIdentifierCandidate_ReturnsCaseInsensitiveExactMatch()
    {
        string[] available =
        {
            "/AmdCpu/0/Temperature/2"
        };

        string? resolved = ClassVUSensors.VU1_SensorManager.ResolveIdentifierCandidate(available, "/amdcpu/0/temperature/2");

        Assert.Equal("/AmdCpu/0/Temperature/2", resolved);
    }

    [Fact]
    public void ResolveIdentifierCandidate_ReturnsSingleStrictCandidate_WhenOnlyOneDomainBusTypeMatchExists()
    {
        string[] available =
        {
            "/nvidia/0/temperature/1",
            "/amdcpu/0/temperature/7"
        };

        string? resolved = ClassVUSensors.VU1_SensorManager.ResolveIdentifierCandidate(available, "/amdcpu/0/temperature/1");

        Assert.Equal("/amdcpu/0/temperature/7", resolved);
    }

    [Fact]
    public void ResolveIdentifierCandidate_BreaksStrictCandidateDistanceTies_Lexicographically()
    {
        string[] available =
        {
            "/amdcpu/0/temperature/3",
            "/amdcpu/0/temperature/1"
        };

        string? resolved = ClassVUSensors.VU1_SensorManager.ResolveIdentifierCandidate(available, "/amdcpu/0/temperature/2");

        Assert.Equal("/amdcpu/0/temperature/1", resolved);
    }

    [Fact]
    public void ResolveIdentifierCandidate_ReturnsSingleTypeCandidate_WhenStrictCandidatesDoNotExist()
    {
        string[] available =
        {
            "/nvidia/0/temperature/1"
        };

        string? resolved = ClassVUSensors.VU1_SensorManager.ResolveIdentifierCandidate(available, "/amdcpu/0/temperature/2");

        Assert.Equal("/nvidia/0/temperature/1", resolved);
    }

    [Fact]
    public void ResolveIdentifierCandidate_ReturnsNull_WhenMultipleTypeCandidatesExist()
    {
        string[] available =
        {
            "/nvidia/0/temperature/1",
            "/intel/0/temperature/4"
        };

        string? resolved = ClassVUSensors.VU1_SensorManager.ResolveIdentifierCandidate(available, "/amdcpu/0/temperature/2");

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveIdentifierCandidate_IgnoresNullAndWhitespaceCandidates()
    {
        string?[] available =
        {
            null,
            "  ",
            "/amdcpu/0/temperature/4"
        };

        string? resolved = ClassVUSensors.VU1_SensorManager.ResolveIdentifierCandidate(available!.Select(item => item!), "/amdcpu/0/temperature/1");

        Assert.Equal("/amdcpu/0/temperature/4", resolved);
    }

    [Fact]
    public void CollectSensorsFromHardware_AddsSupportedSensors_Recursively()
    {
        var manager = new ClassVUSensors.VU1_SensorManager(new List<ClassVUSensors.VU1_Sensor>(), skipHardwareInitialization: true);
        IHardware childHardware = CreateHardware(
            sensors: new[]
            {
                CreateSensor("/amdcpu/0/power/0", SensorType.Power)
            },
            subHardware: Array.Empty<IHardware>());
        IHardware rootHardware = CreateHardware(
            sensors: new[]
            {
                CreateSensor("/amdcpu/0/temperature/2", SensorType.Temperature),
                CreateSensor("/amdcpu/0/fan/0", SensorType.Fan)
            },
            subHardware: new[] { childHardware });

        MethodInfo collectMethod = typeof(ClassVUSensors.VU1_SensorManager)
            .GetMethod("CollectSensorsFromHardware", BindingFlags.Instance | BindingFlags.NonPublic)!;

        collectMethod.Invoke(manager, new object[] { rootHardware });

        List<string> identifiers = manager.get_available_sensors()
            .Select(item => item.Sensor.Identifier.ToString())
            .OrderBy(item => item)
            .ToList();

        Assert.Equal(2, identifiers.Count);
        Assert.Contains("/amdcpu/0/power/0", identifiers);
        Assert.Contains("/amdcpu/0/temperature/2", identifiers);
        Assert.DoesNotContain("/amdcpu/0/fan/0", identifiers);
    }

    private static ISensor CreateSensor(string identifier, SensorType sensorType)
    {
        var proxy = (SensorProxy)(object)DispatchProxy.Create<ISensor, SensorProxy>();
        proxy.IdentifierValue = identifier;
        proxy.SensorTypeValue = sensorType;
        proxy.HardwareValue = CreateHardware(Array.Empty<ISensor>(), Array.Empty<IHardware>());
        return (ISensor)(object)proxy;
    }

    private static Identifier CreateIdentifier(string identifier)
    {
        ConstructorInfo ctor = typeof(Identifier).GetConstructor(new[] { typeof(Identifier), typeof(string[]) })!;
        string[] parts = identifier.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (Identifier)ctor.Invoke(new object?[] { null, parts });
    }

    private static IHardware CreateHardware(IEnumerable<ISensor> sensors, IEnumerable<IHardware> subHardware)
    {
        var proxy = (HardwareProxy)(object)DispatchProxy.Create<IHardware, HardwareProxy>();
        proxy.SensorsValue = sensors.ToArray();
        proxy.SubHardwareValue = subHardware.ToArray();
        return (IHardware)(object)proxy;
    }

    private class SensorProxy : DispatchProxy
    {
        public string IdentifierValue { get; set; } = string.Empty;

        public SensorType SensorTypeValue { get; set; }

        public IHardware HardwareValue { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "get_Identifier":
                    return CreateIdentifier(IdentifierValue);
                case "get_SensorType":
                    return SensorTypeValue;
                case "get_Hardware":
                    return HardwareValue;
                case "get_Value":
                    return null;
            }

            if (targetMethod?.ReturnType == typeof(string))
            {
                return string.Empty;
            }

            if (targetMethod?.ReturnType == typeof(void))
            {
                return null;
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
        public ISensor[] SensorsValue { get; set; } = Array.Empty<ISensor>();

        public IHardware[] SubHardwareValue { get; set; } = Array.Empty<IHardware>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "get_Sensors":
                    return SensorsValue;
                case "get_SubHardware":
                    return SubHardwareValue;
                case "Update":
                    return null;
            }

            if (targetMethod?.ReturnType == typeof(string))
            {
                return string.Empty;
            }

            if (targetMethod?.ReturnType == typeof(void))
            {
                return null;
            }

            if (targetMethod?.ReturnType.IsValueType == true)
            {
                return Activator.CreateInstance(targetMethod.ReturnType);
            }

            return null;
        }
    }
}