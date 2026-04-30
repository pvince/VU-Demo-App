using KR_VU1_Sensors;

namespace VU1WPF.Tests;

public sealed class SensorResolutionTests
{
    [Fact]
    public void ResolveIdentifierCandidate_ReturnsExactMatch_WhenIdentifierExists()
    {
        string[] available =
        {
            "/amdcpu/0/temperature/2",
            "/amdcpu/0/temperature/3"
        };

        string? resolved = ClassVUSensors.VU1_SensorManager.ResolveIdentifierCandidate(available, "/amdcpu/0/temperature/3");

        Assert.Equal("/amdcpu/0/temperature/3", resolved);
    }

    [Fact]
    public void ResolveIdentifierCandidate_ReturnsClosestIndex_WhenExactIdentifierMissing()
    {
        string[] available =
        {
            "/amdcpu/0/temperature/1",
            "/amdcpu/0/temperature/2"
        };

        string? resolved = ClassVUSensors.VU1_SensorManager.ResolveIdentifierCandidate(available, "/amdcpu/0/temperature/9");

        Assert.Equal("/amdcpu/0/temperature/2", resolved);
    }

    [Fact]
    public void ResolveIdentifierCandidate_ReturnsNull_WhenRequestedIdentifierIsInvalid()
    {
        string[] available =
        {
            "/amdcpu/0/temperature/1",
            "/amdcpu/0/temperature/2"
        };

        string? resolved = ClassVUSensors.VU1_SensorManager.ResolveIdentifierCandidate(available, "temperature-only");

        Assert.Null(resolved);
    }
}
