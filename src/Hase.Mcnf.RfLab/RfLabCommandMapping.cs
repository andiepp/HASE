using Hase.Core.Domain.Properties;

namespace Hase.Mcnf.RfLab;

public enum RfLabCommandKind
{
    ApplyCarrier = 0,
    ApplyAmplitudeModulation = 1,
    ApplyFrequencyModulation = 2,
    StartSweep = 3,
    ApplyClock = 4,
    IndicatorControl = 5
}

/// <summary>
/// Maps one parameterless RF-Lab command to the operation it performs with
/// the staged target properties.
/// </summary>
public sealed record RfLabCommandMapping
{
    private RfLabCommandMapping(
        string commandPath,
        string displayName,
        RfLabCommandKind kind,
        RfLabSweepMode? sweepMode = null,
        int? clockChannel = null,
        bool? indicatorEnable = null)
    {
        CommandPath = DescriptorPath.Parse(commandPath);
        DisplayName = displayName;
        Kind = kind;
        SweepMode = sweepMode;
        ClockChannel = clockChannel;
        IndicatorEnable = indicatorEnable;
    }

    public static RfLabCommandMapping ApplyCarrier { get; } = new(
        "Signal.ApplyCarrier",
        "Apply carrier",
        RfLabCommandKind.ApplyCarrier);

    public static RfLabCommandMapping ApplyAmplitudeModulation { get; } = new(
        "Signal.ApplyAmplitudeModulation",
        "Apply amplitude modulation",
        RfLabCommandKind.ApplyAmplitudeModulation);

    public static RfLabCommandMapping ApplyFrequencyModulation { get; } = new(
        "Signal.ApplyFrequencyModulation",
        "Apply frequency modulation",
        RfLabCommandKind.ApplyFrequencyModulation);

    public static RfLabCommandMapping StartSweepBidirectional { get; } = new(
        "Signal.StartSweepBidirectional",
        "Start bidirectional sweep",
        RfLabCommandKind.StartSweep,
        sweepMode: RfLabSweepMode.Bidirectional);

    public static RfLabCommandMapping StartSweepRamp { get; } = new(
        "Signal.StartSweepRamp",
        "Start ramp sweep",
        RfLabCommandKind.StartSweep,
        sweepMode: RfLabSweepMode.Ramp);

    public static RfLabCommandMapping StartSweepSingleRamp { get; } = new(
        "Signal.StartSweepSingleRamp",
        "Start single-ramp sweep",
        RfLabCommandKind.StartSweep,
        sweepMode: RfLabSweepMode.SingleRamp);

    public static RfLabCommandMapping ApplyClock0 { get; } = new(
        "Clock.ApplyOutput0",
        "Apply clock output 0",
        RfLabCommandKind.ApplyClock,
        clockChannel: 0);

    public static RfLabCommandMapping ApplyClock1 { get; } = new(
        "Clock.ApplyOutput1",
        "Apply clock output 1",
        RfLabCommandKind.ApplyClock,
        clockChannel: 1);

    public static RfLabCommandMapping ApplyClock2 { get; } = new(
        "Clock.ApplyOutput2",
        "Apply clock output 2",
        RfLabCommandKind.ApplyClock,
        clockChannel: 2);

    public static RfLabCommandMapping IndicatorOn { get; } = new(
        "Indicator.SwitchOn",
        "Switch indicator on",
        RfLabCommandKind.IndicatorControl,
        indicatorEnable: true);

    public static RfLabCommandMapping IndicatorOff { get; } = new(
        "Indicator.SwitchOff",
        "Switch indicator off",
        RfLabCommandKind.IndicatorControl,
        indicatorEnable: false);

    public static IReadOnlyList<RfLabCommandMapping> All { get; } =
    [
        ApplyCarrier,
        ApplyAmplitudeModulation,
        ApplyFrequencyModulation,
        StartSweepBidirectional,
        StartSweepRamp,
        StartSweepSingleRamp,
        ApplyClock0,
        ApplyClock1,
        ApplyClock2,
        IndicatorOn,
        IndicatorOff
    ];

    public DescriptorPath CommandPath { get; }

    public string DisplayName { get; }

    public RfLabCommandKind Kind { get; }

    public RfLabSweepMode? SweepMode { get; }

    public int? ClockChannel { get; }

    public bool? IndicatorEnable { get; }
}
