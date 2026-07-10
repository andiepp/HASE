namespace Hase.Simulation;

/// <summary>
/// Represents a simulation component that advances with simulation time.
/// </summary>
public interface ISimulation
{
    /// <summary>
    /// Advances the simulation to the supplied simulation step.
    /// </summary>
    void Update(SimulationStep step);
}