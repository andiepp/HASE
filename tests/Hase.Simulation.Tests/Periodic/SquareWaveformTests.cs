using Hase.Simulation.Values.Periodic;

namespace Hase.Simulation.Tests.Values.Periodic;

public sealed class SquareWaveformTests
{
    [Theory]
    [InlineData(0.00, 1.0)]
    [InlineData(0.25, 1.0)]
    [InlineData(0.499999, 1.0)]
    [InlineData(0.50, -1.0)]
    [InlineData(0.75, -1.0)]
    [InlineData(0.999999, -1.0)]
    public void GetValue_ReturnsExpectedValue(
        double phase,
        double expected)
    {
        var actual = SquareWaveform.Instance.GetValue(phase);

        Assert.Equal(expected, actual);
    }
}