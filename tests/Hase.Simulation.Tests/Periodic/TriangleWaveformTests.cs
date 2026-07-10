using Hase.Simulation.Values.Periodic;

namespace Hase.Simulation.Tests.Values.Periodic;

public sealed class TriangleWaveformTests
{
    private const double Tolerance = 1e-12;

    [Theory]
    [InlineData(0.00, 0.0)]
    [InlineData(0.125, 0.5)]
    [InlineData(0.25, 1.0)]
    [InlineData(0.50, 0.0)]
    [InlineData(0.75, -1.0)]
    [InlineData(0.875, -0.5)]
    public void GetValue_ReturnsExpectedValue(
        double phase,
        double expected)
    {
        var actual = TriangleWaveform.Instance.GetValue(phase);

        AssertClose(expected, actual);
    }

    private static void AssertClose(
        double expected,
        double actual)
    {
        Assert.InRange(
            Math.Abs(actual - expected),
            0.0,
            Tolerance);
    }
}