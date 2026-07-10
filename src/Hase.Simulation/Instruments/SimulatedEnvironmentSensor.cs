using Hase.Simulation.Environment;

namespace Hase.Simulation.Instruments;

public sealed class SimulatedEnvironmentSensor
{
    private readonly EnvironmentSimulation _environment;

    public SimulatedEnvironmentSensor(
        EnvironmentSimulation environment)
    {
        _environment = environment
            ?? throw new ArgumentNullException(
                nameof(environment));
    }

    public double Temperature =>
        _environment.CurrentState.Temperature;

    public double RelativeHumidity =>
        _environment.CurrentState.RelativeHumidity;

    public double AirPressure =>
        _environment.CurrentState.AirPressure;
}