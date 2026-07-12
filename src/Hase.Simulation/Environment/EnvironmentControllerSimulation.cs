namespace Hase.Simulation.Environment;

/// <summary>
/// Simulates a writable environment controller.
/// </summary>
public sealed class EnvironmentControllerSimulation
{
    public EnvironmentControllerSimulation(
        EnvironmentControllerState state)
    {
        State = state
            ?? throw new ArgumentNullException(nameof(state));
    }

    /// <summary>
    /// Gets the authoritative controller state.
    /// </summary>
    public EnvironmentControllerState State { get; }

    /// <summary>
    /// Changes the requested target temperature.
    /// </summary>
    public void SetTargetTemperature(
        double targetTemperature)
    {
        State.SetTargetTemperature(targetTemperature);
    }
}
