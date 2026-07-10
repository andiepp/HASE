using Hase.Simulation.Values.Periodic;

namespace Hase.Simulation.Tests.Values.Periodic;

public sealed class PeriodicValueGeneratorTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Constructor_InitializesValueAtSimulationTimeZero()
    {
        var generator = new PeriodicValueGenerator(
            offset: 20.0,
            amplitude: 5.0,
            period: TimeSpan.FromHours(24),
            waveform: SineWaveform.Instance);

        AssertClose(20.0, generator.CurrentValue);
    }

    [Fact]
    public void Update_AppliesOffsetAndAmplitude()
    {
        var generator = new PeriodicValueGenerator(
            offset: 20.0,
            amplitude: 5.0,
            period: TimeSpan.FromHours(24),
            waveform: SineWaveform.Instance);

        generator.Update(
            new SimulationStep(
                elapsed: TimeSpan.FromHours(6),
                simulationTime: TimeSpan.FromHours(6)));

        AssertClose(25.0, generator.CurrentValue);
    }

    [Fact]
    public void Update_UsesTotalSimulationTime_NotElapsedTime()
    {
        var generator = new PeriodicValueGenerator(
            offset: 20.0,
            amplitude: 5.0,
            period: TimeSpan.FromHours(24),
            waveform: SineWaveform.Instance);

        generator.Update(
            new SimulationStep(
                elapsed: TimeSpan.FromMinutes(1),
                simulationTime: TimeSpan.FromHours(6)));

        AssertClose(25.0, generator.CurrentValue);
    }

    [Fact]
    public void Update_WrapsAfterCompletePeriod()
    {
        var generator = new PeriodicValueGenerator(
            offset: 20.0,
            amplitude: 5.0,
            period: TimeSpan.FromHours(24),
            waveform: SineWaveform.Instance);

        generator.Update(
            new SimulationStep(
                elapsed: TimeSpan.FromHours(30),
                simulationTime: TimeSpan.FromHours(30)));

        AssertClose(25.0, generator.CurrentValue);
    }

    [Fact]
    public void FromPhaseRadians_AppliesQuarterCycleOffset()
    {
        var generator =
            PeriodicValueGenerator.FromPhaseRadians(
                offset: 20.0,
                amplitude: 5.0,
                period: TimeSpan.FromHours(24),
                waveform: SineWaveform.Instance,
                phaseRadians: Math.PI / 2.0);

        AssertClose(25.0, generator.CurrentValue);
    }

    [Fact]
    public void FromTimeOffset_AppliesQuarterCycleOffset()
    {
        var generator =
            PeriodicValueGenerator.FromTimeOffset(
                offset: 20.0,
                amplitude: 5.0,
                period: TimeSpan.FromHours(24),
                waveform: SineWaveform.Instance,
                initialTimeOffset: TimeSpan.FromHours(6));

        AssertClose(25.0, generator.CurrentValue);
    }

    [Fact]
    public void FromTimeOffset_WrapsOffsetsLongerThanPeriod()
    {
        var generator =
            PeriodicValueGenerator.FromTimeOffset(
                offset: 20.0,
                amplitude: 5.0,
                period: TimeSpan.FromHours(24),
                waveform: SineWaveform.Instance,
                initialTimeOffset: TimeSpan.FromHours(30));

        AssertClose(25.0, generator.CurrentValue);
    }

    [Fact]
    public void FromTimeOffset_SupportsNegativeOffset()
    {
        var generator =
            PeriodicValueGenerator.FromTimeOffset(
                offset: 20.0,
                amplitude: 5.0,
                period: TimeSpan.FromHours(24),
                waveform: SineWaveform.Instance,
                initialTimeOffset: TimeSpan.FromHours(-6));

        AssertClose(15.0, generator.CurrentValue);
    }

    [Fact]
    public void Constructor_ZeroAmplitude_AlwaysReturnsOffset()
    {
        var generator = new PeriodicValueGenerator(
            offset: 20.0,
            amplitude: 0.0,
            period: TimeSpan.FromHours(24),
            waveform: SineWaveform.Instance);

        generator.Update(
            new SimulationStep(
                elapsed: TimeSpan.FromHours(8),
                simulationTime: TimeSpan.FromHours(8)));

        AssertClose(20.0, generator.CurrentValue);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_InvalidAmplitude_Throws(
        double amplitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PeriodicValueGenerator(
                offset: 20.0,
                amplitude: amplitude,
                period: TimeSpan.FromHours(24),
                waveform: SineWaveform.Instance));
    }

    [Fact]
    public void Constructor_ZeroPeriod_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PeriodicValueGenerator(
                offset: 20.0,
                amplitude: 5.0,
                period: TimeSpan.Zero,
                waveform: SineWaveform.Instance));
    }

    [Fact]
    public void Constructor_NullWaveform_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PeriodicValueGenerator(
                offset: 20.0,
                amplitude: 5.0,
                period: TimeSpan.FromHours(24),
                waveform: null!));
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