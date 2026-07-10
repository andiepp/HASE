namespace Hase.Simulation.Values;

/// <summary>
/// Generates a value as simulation time advances.
/// </summary>
public interface IValueGenerator
{
    /// <summary>
    /// Gets the currently generated value.
    /// </summary>
    double CurrentValue { get; }

    /// <summary>
    /// Updates the generated value for the supplied simulation step.
    /// </summary>
    void Update(SimulationStep step);
}