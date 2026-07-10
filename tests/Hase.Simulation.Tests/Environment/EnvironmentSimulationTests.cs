using Hase.Simulation.Environment;
using Hase.Simulation.Values;
using Hase.Simulation.Values.Periodic;

namespace Hase.Simulation.Tests.Environment;

public sealed class EnvironmentSimulationTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Constructor_CreatesInitialState()
    {
        var simulation = new EnvironmentSimulation(
            new ConstantValueGenerator(20.0),
            new ConstantValueGenerator(55.0),
            new ConstantValueGenerator(1013.0));

        AssertClose(
            20.0,
            simulation.CurrentState.Temperature);

        AssertClose(
            55.0,
            simulation.CurrentState.RelativeHumidity);

        AssertClose(
            1013.0,
            simulation.CurrentState.AirPressure);
    }

    [Fact]
    public void Update_UpdatesAllEnvironmentalValues()
    {
        var temperature =
            new PeriodicValueGenerator(
                offset: 20.0,
                amplitude: 5.0,
                period: TimeSpan.FromHours(24),
                waveform: SineWaveform.Instance);

        var humidity =
            new PeriodicValueGenerator(
                offset: 60.0,
                amplitude: 10.0,
                period: TimeSpan.FromHours(24),
                waveform: SineWaveform.Instance);

        var pressure =
            new ConstantValueGenerator(1013.0);

        var simulation = new EnvironmentSimulation(
            temperature,
            humidity,
            pressure);

        simulation.Update(
            new SimulationStep(
                elapsed: TimeSpan.FromHours(6),
                simulationTime: TimeSpan.FromHours(6)));

        AssertClose(
            25.0,
            simulation.CurrentState.Temperature);

        AssertClose(
            70.0,
            simulation.CurrentState.RelativeHumidity);

        AssertClose(
            1013.0,
            simulation.CurrentState.AirPressure);
    }

    [Fact]
    public void Update_ReplacesStateInstance()
    {
        var simulation = new EnvironmentSimulation(
            new ConstantValueGenerator(20.0),
            new ConstantValueGenerator(55.0),
            new ConstantValueGenerator(1013.0));

        var previousState = simulation.CurrentState;

        simulation.Update(
            new SimulationStep(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1)));

        Assert.NotSame(
            previousState,
            simulation.CurrentState);
    }

    [Fact]
    public void Constructor_NullGenerator_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new EnvironmentSimulation(
                null!,
                new ConstantValueGenerator(55.0),
                new ConstantValueGenerator(1013.0)));
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