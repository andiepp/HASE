using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103;

public sealed record Kel103ModeSelectionMapping
{
    private Kel103ModeSelectionMapping(
        DescriptorPath commandPath,
        Kel103OperatingMode mode,
        string command,
        string expectedReadbackToken)
    {
        CommandPath = commandPath;
        Mode = mode;
        Command = command;
        ExpectedReadbackToken = expectedReadbackToken;
    }

    public static Kel103ModeSelectionMapping ConstantCurrent { get; } = new(
        DescriptorPath.Parse("Mode.SelectConstantCurrent"),
        Kel103OperatingMode.ConstantCurrent,
        ":FUNCtion CC",
        "CC");

    public static Kel103ModeSelectionMapping ConstantVoltage { get; } = new(
        DescriptorPath.Parse("Mode.SelectConstantVoltage"),
        Kel103OperatingMode.ConstantVoltage,
        ":FUNCtion CV",
        "CV");

    public static Kel103ModeSelectionMapping ConstantResistance { get; } = new(
        DescriptorPath.Parse("Mode.SelectConstantResistance"),
        Kel103OperatingMode.ConstantResistance,
        ":FUNCtion CR",
        "CR");

    public static Kel103ModeSelectionMapping ConstantPower { get; } = new(
        DescriptorPath.Parse("Mode.SelectConstantPower"),
        Kel103OperatingMode.ConstantPower,
        ":FUNCtion CW",
        "CW");

    public static Kel103ModeSelectionMapping ShortCircuit { get; } = new(
        DescriptorPath.Parse("Mode.SelectShortCircuit"),
        Kel103OperatingMode.ShortCircuit,
        ":FUNCtion SHORt",
        "SHORt");

    public static IReadOnlyList<Kel103ModeSelectionMapping> All { get; } =
        Array.AsReadOnly<Kel103ModeSelectionMapping>(
        [
            ConstantCurrent,
            ConstantVoltage,
            ConstantResistance,
            ConstantPower,
            ShortCircuit
        ]);

    public DescriptorPath CommandPath { get; }

    public Kel103OperatingMode Mode { get; }

    public string Command { get; }

    public string ExpectedReadbackToken { get; }
}
