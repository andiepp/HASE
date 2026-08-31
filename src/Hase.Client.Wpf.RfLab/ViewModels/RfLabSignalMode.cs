#nullable enable

namespace Hase.Client.Wpf.RfLab.ViewModels;

/// <summary>
/// The operating modes the RF-Lab panel offers, in the order and numbering the
/// original application's mode selector used.
/// </summary>
public enum RfLabSignalMode
{
    Off = 0,
    AmplitudeModulation = 1,
    FrequencyModulation = 2,
    Sweep = 3,

    /// <summary>
    /// Steps the carrier across the sweep span and reads the detector at
    /// every step, so the panel plots a scalar response over frequency.
    /// </summary>
    Analyze = 4,

    Measure = 5
}
