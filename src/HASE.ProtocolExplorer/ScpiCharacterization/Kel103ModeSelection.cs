namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal enum Kel103ModeSelection
{
    ConstantCurrent = 0,
    ConstantVoltage = 1,
    ConstantResistance = 2,
    ConstantPower = 3,
    ShortCircuit = 4
}

internal static class Kel103ModeSelectionExtensions
{
    public static string ToArgumentValue(this Kel103ModeSelection selection) =>
        selection switch
        {
            Kel103ModeSelection.ConstantCurrent => "cc",
            Kel103ModeSelection.ConstantVoltage => "cv",
            Kel103ModeSelection.ConstantResistance => "cr",
            Kel103ModeSelection.ConstantPower => "cw",
            Kel103ModeSelection.ShortCircuit => "short",
            _ => throw new ArgumentOutOfRangeException(nameof(selection))
        };

    public static string ToCommandText(this Kel103ModeSelection selection) =>
        selection switch
        {
            Kel103ModeSelection.ConstantCurrent => ":FUNCtion CC",
            Kel103ModeSelection.ConstantVoltage => ":FUNCtion CV",
            Kel103ModeSelection.ConstantResistance => ":FUNCtion CR",
            Kel103ModeSelection.ConstantPower => ":FUNCtion CW",
            Kel103ModeSelection.ShortCircuit => ":FUNCtion SHORt",
            _ => throw new ArgumentOutOfRangeException(nameof(selection))
        };

    public static string ToReadbackToken(this Kel103ModeSelection selection) =>
        selection switch
        {
            Kel103ModeSelection.ConstantCurrent => "CC",
            Kel103ModeSelection.ConstantVoltage => "CV",
            Kel103ModeSelection.ConstantResistance => "CR",
            Kel103ModeSelection.ConstantPower => "CW",
            Kel103ModeSelection.ShortCircuit => "SHORt",
            _ => throw new ArgumentOutOfRangeException(nameof(selection))
        };
}
