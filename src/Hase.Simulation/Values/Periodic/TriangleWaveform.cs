namespace Hase.Simulation.Values.Periodic;

/// <summary>
/// A symmetric triangle waveform starting at zero and initially rising.
/// </summary>
public sealed class TriangleWaveform : IPeriodicWaveform
{
    private TriangleWaveform()
    {
    }

    public static TriangleWaveform Instance { get; } = new();

    public double GetValue(double phase)
    {
        ValidatePhase(phase);

        return phase switch
        {
            < 0.25 => 4.0 * phase,
            < 0.75 => 2.0 - 4.0 * phase,
            _ => 4.0 * phase - 4.0
        };
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