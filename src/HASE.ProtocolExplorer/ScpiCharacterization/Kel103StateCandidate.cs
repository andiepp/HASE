namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal enum Kel103StateCandidate
{
    Mode = 0,
    InputState = 1,
    TargetVoltage = 2,
    TargetCurrent = 3,
    TargetResistance = 4,
    TargetPower = 5
}

internal static class Kel103StateCandidateExtensions
{
    public static string ToArgumentValue(this Kel103StateCandidate candidate) =>
        candidate switch
        {
            Kel103StateCandidate.Mode => "mode",
            Kel103StateCandidate.InputState => "input-state",
            Kel103StateCandidate.TargetVoltage => "target-voltage",
            Kel103StateCandidate.TargetCurrent => "target-current",
            Kel103StateCandidate.TargetResistance => "target-resistance",
            Kel103StateCandidate.TargetPower => "target-power",
            _ => throw Unsupported(candidate)
        };

    public static string ToQueryText(this Kel103StateCandidate candidate) =>
        candidate switch
        {
            Kel103StateCandidate.Mode => ":FUNCtion?",
            Kel103StateCandidate.InputState => ":INPut?",
            Kel103StateCandidate.TargetVoltage => ":VOLTage?",
            Kel103StateCandidate.TargetCurrent => ":CURRent?",
            Kel103StateCandidate.TargetResistance => ":RESistance?",
            Kel103StateCandidate.TargetPower => ":POWer?",
            _ => throw Unsupported(candidate)
        };

    public static string? ToUnitSymbol(this Kel103StateCandidate candidate) =>
        candidate switch
        {
            Kel103StateCandidate.Mode or Kel103StateCandidate.InputState => null,
            Kel103StateCandidate.TargetVoltage => "V",
            Kel103StateCandidate.TargetCurrent => "A",
            Kel103StateCandidate.TargetResistance => "OHM",
            Kel103StateCandidate.TargetPower => "W",
            _ => throw Unsupported(candidate)
        };

    private static ArgumentOutOfRangeException Unsupported(Kel103StateCandidate candidate) =>
        new(
            nameof(candidate),
            candidate,
            "The KEL-103 state candidate is not supported.");
}
