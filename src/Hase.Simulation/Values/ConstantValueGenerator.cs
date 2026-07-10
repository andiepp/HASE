namespace Hase.Simulation.Values;

/// <summary>
/// Provides a constant value independent of simulation time.
/// </summary>
public sealed class ConstantValueGenerator : IValueGenerator
{
    public ConstantValueGenerator(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "The value must be finite.");
        }

        CurrentValue = value;
    }

    public double CurrentValue { get; }

    public void Update(SimulationStep step)
    {
        // The value remains constant.
    }
}