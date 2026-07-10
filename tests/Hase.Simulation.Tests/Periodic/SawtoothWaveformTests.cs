using Hase.Simulation.Values.Periodic;

namespace Hase.Simulation.Tests.Values.Periodic;

public sealed class SawtoothWaveformTests
{
    private const double Tolerance = 1e-12;

    [Theory]
    [InlineData(0.00, -1.0)]
    [InlineData(0.25, -0.5)]
    [InlineData(0.50, 0.0)]
    [InlineData(0.75, 0.5)]
    public void GetValue_ReturnsExpectedValue(
        double phase,
        double expected)
    {
        var actual = SawtoothWaveform.Instance.GetValue(phase);

        Assert.InRange(
            Math.Abs(actual - expected),
            0.0,
            Tolerance);
    }

    [Fact]
    public void GetValue_ImmediatelyBeforeCycleEnd_ApproachesOne()
    {
        const double phase = 0.999999;

        var actual = SawtoothWaveform.Instance.GetValue(phase);

        Assert.True(actual < 1.0);
        Assert.True(actual > 0.999);
    }
}