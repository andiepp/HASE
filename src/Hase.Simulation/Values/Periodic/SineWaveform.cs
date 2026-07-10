namespace Hase.Simulation.Values.Periodic;

/// <summary>
/// A sinusoidal waveform starting at zero and initially rising.
/// </summary>
public sealed class SineWaveform : IPeriodicWaveform
{
    private SineWaveform()
    {
    }

    public static SineWaveform Instance { get; } = new();

    public double GetValue(double phase)
    {
        ValidatePhase(phase);

        return Math.Sin(2.0 * Math.PI * phase);
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