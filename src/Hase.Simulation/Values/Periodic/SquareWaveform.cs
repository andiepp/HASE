namespace Hase.Simulation.Values.Periodic;

/// <summary>
/// A square waveform with a fixed 50 percent duty cycle.
/// </summary>
public sealed class SquareWaveform : IPeriodicWaveform
{
    private SquareWaveform()
    {
    }

    public static SquareWaveform Instance { get; } = new();

    public double GetValue(double phase)
    {
        ValidatePhase(phase);

        return phase < 0.5 ? 1.0 : -1.0;
    }

    private static void ValidatePhase(double phase)
    {
        if (!double.IsFinite(phase) || phase < 0.0 || phase >= 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(phase),
                "Phase must be finite and in the range [0, 1).");
        }
    }
}