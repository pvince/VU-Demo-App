using VU1WPF;

namespace VU1WPF.Tests;

public sealed class ScalingValidationTests
{
    [Theory]
    [InlineData(0f, 100f)]
    [InlineData(20f, 100f)]
    [InlineData(-10f, 10f)]
    [InlineData(0.001f, 0.002f)]
    public void AreScalingBoundsValid_ReturnsTrue_WhenMinStrictlyLessThanMax(float min, float max)
    {
        bool valid = DialComputationEngine.AreScalingBoundsValid(min, max, out string reason);

        Assert.True(valid);
        Assert.Equal(String.Empty, reason);
    }

    [Theory]
    [InlineData(100f, 100f)]
    [InlineData(0f, 0f)]
    [InlineData(50f, 50f)]
    public void AreScalingBoundsValid_ReturnsFalse_WhenMinEqualsMax(float min, float max)
    {
        bool valid = DialComputationEngine.AreScalingBoundsValid(min, max, out string reason);

        Assert.False(valid);
        Assert.False(String.IsNullOrWhiteSpace(reason));
        Assert.Contains("equal", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(100f, 20f)]
    [InlineData(50f, 0f)]
    [InlineData(1f, 0f)]
    public void AreScalingBoundsValid_ReturnsFalse_WhenMinGreaterThanMax(float min, float max)
    {
        bool valid = DialComputationEngine.AreScalingBoundsValid(min, max, out string reason);

        Assert.False(valid);
        Assert.False(String.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void ComputeDialValuePercent_WithEqualBounds_ReturnsZero()
    {
        int result = DialComputationEngine.ComputeDialValuePercent(50f, 50f, 50f);

        Assert.Equal(0, result);
    }
}
