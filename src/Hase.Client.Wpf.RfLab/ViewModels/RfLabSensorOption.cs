#nullable enable

namespace Hase.Client.Wpf.RfLab.ViewModels;

/// <summary>
/// One selectable detector reading of the RF-Lab panel.
/// </summary>
/// <remarks>
/// The Runtime Host performs the physical conversion and publishes both the
/// detector level and the raw detector voltage as Properties. A sensor option
/// therefore names a published Property and the axis bounds to present it
/// with; the panel performs no sensor arithmetic of its own.
/// </remarks>
public sealed record RfLabSensorOption(
    string Name,
    string PropertyId,
    string Units,
    double Minimum,
    double Maximum)
{
    /// <summary>
    /// Gets whether this reading offers a calibration action. The published
    /// readings are converted by the Runtime Host and need none.
    /// </summary>
    public bool NeedToBeCalibrated => false;

    public override string ToString() => Name;
}
