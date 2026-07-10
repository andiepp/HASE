namespace Hase.Simulation;

public readonly record struct SimulationStep
{
    public SimulationStep(
        TimeSpan elapsed,
        TimeSpan simulationTime)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsed));
        }

        if (simulationTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(simulationTime));
        }

        Elapsed = elapsed;
        SimulationTime = simulationTime;
    }

    public TimeSpan Elapsed { get; }

    public TimeSpan SimulationTime { get; }
}