using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Mcnf.RfLab;

/// <summary>
/// Maps one writable RF-Lab target property to its unit, characterized
/// range, and host-side default. Targets are staged on the host and pushed
/// to the node by the apply commands; the node offers no state readback.
/// </summary>
public sealed record RfLabTargetMapping
{
    private RfLabTargetMapping(
        string propertyId,
        string propertyPath,
        Quantity quantity,
        Unit unit,
        double minimum,
        double maximum,
        double defaultValue,
        int? clockChannel = null)
    {
        PropertyId = new PropertyId(propertyId);
        PropertyPath = DescriptorPath.Parse(propertyPath);
        Quantity = quantity;
        Unit = unit;
        Minimum = minimum;
        Maximum = maximum;
        DefaultValue = defaultValue;
        ClockChannel = clockChannel;
    }

    public static RfLabTargetMapping Frequency { get; } = new(
        "target-frequency", "Target.Frequency",
        Quantities.Frequency, Units.Hertz,
        RfLabRanges.CarrierFrequencyMinimum, RfLabRanges.CarrierFrequencyMaximum,
        defaultValue: 10_000_000);

    public static RfLabTargetMapping Attenuation { get; } = new(
        "target-attenuation", "Target.Attenuation",
        RfLabUnits.Attenuation, RfLabUnits.DecibelAttenuation,
        RfLabRanges.AttenuationMinimum, RfLabRanges.AttenuationMaximum,
        defaultValue: 20);

    public static RfLabTargetMapping ModulationFrequency { get; } = new(
        "modulation-frequency", "Target.ModulationFrequency",
        Quantities.Frequency, Units.Hertz,
        RfLabRanges.ModulationFrequencyMinimum, RfLabRanges.ModulationFrequencyMaximum,
        defaultValue: 1_000);

    public static RfLabTargetMapping AmplitudeModulationDepth { get; } = new(
        "am-depth", "Target.AmplitudeModulationDepth",
        RfLabUnits.Ratio, RfLabUnits.Percent,
        RfLabRanges.AmplitudeModulationDepthMinimum, RfLabRanges.AmplitudeModulationDepthMaximum,
        defaultValue: 80);

    public static RfLabTargetMapping FrequencyModulationDeviation { get; } = new(
        "fm-deviation", "Target.FrequencyModulationDeviation",
        Quantities.Frequency, Units.Hertz,
        RfLabRanges.FrequencyModulationDeviationMinimum, RfLabRanges.FrequencyModulationDeviationMaximum,
        defaultValue: 10_000);

    public static RfLabTargetMapping SweepStartFrequency { get; } = new(
        "sweep-start-frequency", "Target.SweepStartFrequency",
        Quantities.Frequency, Units.Hertz,
        RfLabRanges.CarrierFrequencyMinimum, RfLabRanges.CarrierFrequencyMaximum,
        defaultValue: 10_000_000);

    public static RfLabTargetMapping SweepStopFrequency { get; } = new(
        "sweep-stop-frequency", "Target.SweepStopFrequency",
        Quantities.Frequency, Units.Hertz,
        RfLabRanges.CarrierFrequencyMinimum, RfLabRanges.CarrierFrequencyMaximum,
        defaultValue: 30_000_000);

    public static RfLabTargetMapping SweepTime { get; } = new(
        "sweep-time", "Target.SweepTime",
        Quantities.Time, RfLabUnits.Millisecond,
        RfLabRanges.SweepTimeMillisecondsMinimum, RfLabRanges.SweepTimeMillisecondsMaximum,
        defaultValue: 2_000);

    public static RfLabTargetMapping Clock0Frequency { get; } = new(
        "clock0-frequency", "Clock.Output0Frequency",
        Quantities.Frequency, Units.Hertz,
        RfLabRanges.ClockFrequencyMinimum, RfLabRanges.ClockFrequencyMaximum,
        defaultValue: 1_000_000,
        clockChannel: 0);

    public static RfLabTargetMapping Clock1Frequency { get; } = new(
        "clock1-frequency", "Clock.Output1Frequency",
        Quantities.Frequency, Units.Hertz,
        RfLabRanges.ClockFrequencyMinimum, RfLabRanges.ClockFrequencyMaximum,
        defaultValue: 2_000_000,
        clockChannel: 1);

    public static RfLabTargetMapping Clock2Frequency { get; } = new(
        "clock2-frequency", "Clock.Output2Frequency",
        Quantities.Frequency, Units.Hertz,
        RfLabRanges.ClockFrequencyMinimum, RfLabRanges.ClockFrequencyMaximum,
        defaultValue: 3_000_000,
        clockChannel: 2);

    public static IReadOnlyList<RfLabTargetMapping> All { get; } =
    [
        Frequency,
        Attenuation,
        ModulationFrequency,
        AmplitudeModulationDepth,
        FrequencyModulationDeviation,
        SweepStartFrequency,
        SweepStopFrequency,
        SweepTime,
        Clock0Frequency,
        Clock1Frequency,
        Clock2Frequency
    ];

    public PropertyId PropertyId { get; }

    public DescriptorPath PropertyPath { get; }

    public Quantity Quantity { get; }

    public Unit Unit { get; }

    public double Minimum { get; }

    public double Maximum { get; }

    public double DefaultValue { get; }

    /// <summary>
    /// Gets the Si5351 clock channel this target feeds, or null for a
    /// DDS target.
    /// </summary>
    public int? ClockChannel { get; }
}
