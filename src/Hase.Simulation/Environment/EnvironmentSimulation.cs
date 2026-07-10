using Hase.Simulation.Values;

namespace Hase.Simulation.Environment;

/// <summary>
/// Simulates environmental conditions using independent value generators.
/// </summary>
public sealed class EnvironmentSimulation : ISimulation
{
    public EnvironmentSimulation(
        IValueGenerator temperatureGenerator,
        IValueGenerator relativeHumidityGenerator,
        IValueGenerator airPressureGenerator)
    {
        TemperatureGenerator =
            temperatureGenerator
            ?? throw new ArgumentNullException(
                nameof(temperatureGenerator));

        RelativeHumidityGenerator =
            relativeHumidityGenerator
            ?? throw new ArgumentNullException(
                nameof(relativeHumidityGenerator));

        AirPressureGenerator =
            airPressureGenerator
            ?? throw new ArgumentNullException(
                nameof(airPressureGenerator));

        CurrentState = CreateState();
    }

    public IValueGenerator TemperatureGenerator { get; }

    public IValueGenerator RelativeHumidityGenerator { get; }

    public IValueGenerator AirPressureGenerator { get; }

    public EnvironmentState CurrentState { get; private set; }

    public void Update(SimulationStep step)
    {
        TemperatureGenerator.Update(step);
        RelativeHumidityGenerator.Update(step);
        AirPressureGenerator.Update(step);

        CurrentState = CreateState();
    }

    private EnvironmentState CreateState()
    {
        return new EnvironmentState(
            Temperature: TemperatureGenerator.CurrentValue,
            RelativeHumidity:
                RelativeHumidityGenerator.CurrentValue,
            AirPressure:
                AirPressureGenerator.CurrentValue);
    }
}