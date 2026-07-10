namespace Hase.Simulation.Values.Periodic;

/// <summary>
/// Defines the normalized shape of one periodic cycle.
/// </summary>
public interface IPeriodicWaveform
{
    /// <summary>
    /// Gets the normalized waveform value for a phase within one cycle.
    /// </summary>
    /// <param name="phase">
    /// Phase within the cycle in the range [0, 1).
    /// </param>
    /// <returns>
    /// The normalized waveform value, normally in the range [-1, +1].
    /// </returns>
    double GetValue(double phase);
}