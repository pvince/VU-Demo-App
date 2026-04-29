using System.Collections.Generic;
using VU1WPF;

namespace VU1WPF.Tests;

public class DialComputationTests
{
    [Theory]
    [InlineData(0f, 100f, 40f, 40)]
    [InlineData(50f, 50f, 200f, 0)]
    [InlineData(0f, 100f, 0f, 0)]
    [InlineData(0f, 100f, 100f, 100)]
    [InlineData(10f, 110f, 60f, 50)]
    public void ComputeDialValue_ReturnsExpectedPercent(float min, float max, float sensorValue, int expected)
    {
        int actual = DialComputationEngine.ComputeDialValuePercent(min, max, sensorValue);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolveThresholdColor_ReturnsFirstThresholdGreaterOrEqualToValue()
    {
        var thresholds = new List<ClassDialThreshold>
        {
            new() { Threshold = 25, BacklightRed = 1, BacklightGreen = 2, BacklightBlue = 3 },
            new() { Threshold = 50, BacklightRed = 4, BacklightGreen = 5, BacklightBlue = 6 },
            new() { Threshold = 75, BacklightRed = 7, BacklightGreen = 8, BacklightBlue = 9 },
        };

        var match = DialComputationEngine.ResolveThresholdColor(thresholds, 49);

        Assert.NotNull(match);
        Assert.Equal(50, match!.Threshold);
        Assert.Equal(4, match.BacklightRed);
        Assert.Equal(5, match.BacklightGreen);
        Assert.Equal(6, match.BacklightBlue);
    }
}
