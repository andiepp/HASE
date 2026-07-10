namespace Hase.Simulation.Values.Periodic;

/// <summary>
/// A rising sawtooth waveform from -1 toward +1.
/// </summary>
public sealed class SawtoothWaveform : IPeriodicWaveform
{
    private SawtoothWaveform()
    {
    }

    public static SawtoothWaveform Instance { get; } = new();

    public double GetValue(double phase)
    {
        ValidatePhase(phase);

        return 2.0 * phase - 1.0;
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