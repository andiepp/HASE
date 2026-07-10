using Hase.Simulation.Values.Periodic;

namespace Hase.Simulation.Tests.Values.Periodic;

public sealed class SineWaveformTests
{
    private const double Tolerance = 1e-12;

    [Theory]
    [InlineData(0.00, 0.0)]
    [InlineData(0.25, 1.0)]
    [InlineData(0.50, 0.0)]
    [InlineData(0.75, -1.0)]
    public void GetValue_ReturnsExpectedValue(
        double phase,
        double expected)
    {
        var actual = SineWaveform.Instance.GetValue(phase);

        Assert.Equal(expected, actual, Tolerance);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.00)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void GetValue_InvalidPhase_Throws(double phase)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SineWaveform.Instance.GetValue(phase));
    }
}