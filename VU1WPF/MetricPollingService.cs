using LibreHardwareMonitor.Hardware;
using System;

namespace VU1WPF
{
    public enum SensorReadingStatus
    {
        Available,
        Unavailable,
        Invalid
    }

    public readonly struct SensorReadingResult
    {
        public SensorReadingStatus Status { get; }
        public float? Value { get; }
        public string Reason { get; }

        private SensorReadingResult(SensorReadingStatus status, float? value, string reason)
        {
            Status = status;
            Value = value;
            Reason = reason;
        }

        public static SensorReadingResult Available(float value)
        {
            return new SensorReadingResult(SensorReadingStatus.Available, value, String.Empty);
        }

        public static SensorReadingResult Unavailable(string reason)
        {
            return new SensorReadingResult(SensorReadingStatus.Unavailable, null, reason);
        }

        public static SensorReadingResult Invalid(string reason)
        {
            return new SensorReadingResult(SensorReadingStatus.Invalid, null, reason);
        }
    }

    public static class MetricPollingService
    {
        private static void UpdateHardwareChain(IHardware hardware)
        {
            if (hardware.Parent != null)
            {
                UpdateHardwareChain(hardware.Parent);
            }

            hardware.Update();
        }

        public static SensorReadingResult ReadSensorValue(ISensor? sensor)
        {
            if (sensor == null)
            {
                return SensorReadingResult.Unavailable("sensor_not_bound");
            }

            try
            {
                UpdateHardwareChain(sensor.Hardware);
                float? raw = sensor.Value;
                if (!raw.HasValue)
                {
                    return SensorReadingResult.Unavailable("sensor_value_null");
                }

                if (float.IsNaN(raw.Value) || float.IsInfinity(raw.Value))
                {
                    return SensorReadingResult.Invalid("sensor_value_non_finite");
                }

                return SensorReadingResult.Available(raw.Value);
            }
            catch (Exception ex)
            {
                return SensorReadingResult.Invalid($"sensor_exception_{ex.GetType().Name}");
            }
        }
    }
}