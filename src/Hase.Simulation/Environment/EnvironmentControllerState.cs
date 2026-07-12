namespace Hase.Simulation.Environment;

/// <summary>
/// Represents the authoritative writable state of a simulated
/// environment controller.
/// </summary>
public sealed class EnvironmentControllerState
{
    public EnvironmentControllerState(
        double targetTemperature)
    {
        TargetTemperature = targetTemperature;
    }

    /// <summary>
    /// Gets the requested target temperature in degrees Celsius.
    /// </summary>
    public double TargetTemperature { get; private set; }

    /// <summary>
    /// Changes the requested target temperature.
    /// </summary>
    public void SetTargetTemperature(
        double targetTemperature)
    {
        TargetTemperature = targetTemperature;
    }
}