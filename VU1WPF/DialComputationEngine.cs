using System;
using System.Collections.Generic;

namespace VU1WPF
{
    public static class DialComputationEngine
    {
        public static int ComputeDialValuePercent(float scaleMin, float scaleMax, float sensorValue)
        {
            if (scaleMin == scaleMax || sensorValue <= scaleMin)
            {
                return 0;
            }

            if (sensorValue >= scaleMax || scaleMax == 0)
            {
                return 100;
            }

            float scaledValue = ((sensorValue - scaleMin) / (scaleMax - scaleMin)) * 100;

            if (scaledValue < 0)
            {
                scaledValue = 0;
            }
            else if (scaledValue > 100)
            {
                scaledValue = 100;
            }

            return (int)Math.Round(scaledValue);
        }

        public static bool AreScalingBoundsValid(float scaleMin, float scaleMax, out string reason)
        {
            if (scaleMin >= scaleMax)
            {
                reason = scaleMin == scaleMax
                    ? "Min and Max must not be equal"
                    : "Min must be less than Max";
                return false;
            }

            reason = String.Empty;
            return true;
        }

        public static ClassDialThreshold? ResolveThresholdColor(List<ClassDialThreshold>? thresholds, int dialValue)
        {
            if (thresholds == null || thresholds.Count == 0)
            {
                return null;
            }

            return thresholds.Find(item => item.Threshold >= dialValue);
        }
    }
}