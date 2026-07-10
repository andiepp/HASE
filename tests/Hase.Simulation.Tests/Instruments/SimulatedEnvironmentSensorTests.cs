using Hase.Simulation.Environment;
using Hase.Simulation.Instruments;
using Hase.Simulation.Values;
using Hase.Simulation.Values.Periodic;

namespace Hase.Simulation.Tests.Instruments;

public sealed class SimulatedEnvironmentSensorTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Constructor_NullEnvironment_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SimulatedEnvironmentSensor(null!));
    }

    [Fact]
    public void Properties_InitiallyReflectEnvironmentState()
    {
        var environment = new EnvironmentSimulation(
            new ConstantValueGenerator(21.5),
            new ConstantValueGenerator(58.0),
            new ConstantValueGenerator(1012.5));

        var sensor =
            new SimulatedEnvironmentSensor(environment);

        AssertClose(21.5, sensor.Temperature);
        AssertClose(58.0, sensor.RelativeHumidity);
        AssertClose(1012.5, sensor.AirPressure);
    }

    [Fact]
    public void Properties_ReflectUpdatedEnvironmentState()
    {
        var temperatureGenerator =
            new PeriodicValueGenerator(
                offset: 20.0,
                amplitude: 5.0,
                period: TimeSpan.FromHours(24),
                waveform: SineWaveform.Instance);

        var humidityGenerator =
            PeriodicValueGenerator.FromTimeOffset(
                offset: 60.0,
                amplitude: 10.0,
                period: TimeSpan.FromHours(24),
                waveform: SineWaveform.Instance,
                initialTimeOffset: TimeSpan.FromHours(12));

        var pressureGenerator =
            new ConstantValueGenerator(1013.0);

        var environment = new EnvironmentSimulation(
            temperatureGenerator,
            humidityGenerator,
            pressureGenerator);

        var sensor =
            new SimulatedEnvironmentSensor(environment);

        environment.Update(
            new SimulationStep(
                elapsed: TimeSpan.FromHours(6),
                simulationTime: TimeSpan.FromHours(6)));

        AssertClose(25.0, sensor.Temperature);
        AssertClose(50.0, sensor.RelativeHumidity);
        AssertClose(1013.0, sensor.AirPressure);
    }

    [Fact]
    public void Sensor_DoesNotCacheEnvironmentValues()
    {
        var temperatureGenerator =
            new PeriodicValueGenerator(
                offset: 20.0,
                amplitude: 5.0,
                period: TimeSpan.FromHours(24),
                waveform: SineWaveform.Instance);

        var environment = new EnvironmentSimulation(
            temperatureGenerator,
            new ConstantValueGenerator(55.0),
            new ConstantValueGenerator(1013.0));

        var sensor =
            new SimulatedEnvironmentSensor(environment);

        AssertClose(20.0, sensor.Temperature);

        environment.Update(
            new SimulationStep(
                elapsed: TimeSpan.FromHours(6),
                simulationTime: TimeSpan.FromHours(6)));

        AssertClose(25.0, sensor.Temperature);

        environment.Update(
            new SimulationStep(
                elapsed: TimeSpan.FromHours(6),
                simulationTime: TimeSpan.FromHours(12)));

        AssertClose(20.0, sensor.Temperature);
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