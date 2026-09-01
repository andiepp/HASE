namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal enum Kel103SetpointLimit
{
    Lower = 0,
    Upper = 1
}

internal static class Kel103SetpointLimitExtensions
{
    public static string ToArgumentValue(this Kel103SetpointLimit limit) =>
        limit switch
        {
            Kel103SetpointLimit.Lower => "lower",
            Kel103SetpointLimit.Upper => "upper",
            _ => throw Unsupported(limit)
        };

    public static string ToQueryText(
        this Kel103SetpointLimit limit,
        Kel103StateCandidate candidate)
    {
        EnsureSetpointCandidate(candidate);

        return (candidate, limit) switch
        {
            (Kel103StateCandidate.TargetVoltage, Kel103SetpointLimit.Lower) => ":VOLT:LOW?",
            (Kel103StateCandidate.TargetVoltage, Kel103SetpointLimit.Upper) => ":VOLT:UPP?",
            (Kel103StateCandidate.TargetCurrent, Kel103SetpointLimit.Lower) => ":CURR:LOW?",
            (Kel103StateCandidate.TargetCurrent, Kel103SetpointLimit.Upper) => ":CURR:UPP?",
            (Kel103StateCandidate.TargetResistance, Kel103SetpointLimit.Lower) => ":RES:LOW?",
            (Kel103StateCandidate.TargetResistance, Kel103SetpointLimit.Upper) => ":RES:UPP?",
            (Kel103StateCandidate.TargetPower, Kel103SetpointLimit.Lower) => ":POW:LOW?",
            (Kel103StateCandidate.TargetPower, Kel103SetpointLimit.Upper) => ":POW:UPP?",
            _ => throw Unsupported(limit)
        };
    }

    public static void EnsureSetpointCandidate(Kel103StateCandidate candidate)
    {
        if (candidate is not (
            Kel103StateCandidate.TargetVoltage
            or Kel103StateCandidate.TargetCurrent
            or Kel103StateCandidate.TargetResistance
            or Kel103StateCandidate.TargetPower))
        {
            throw new ArgumentOutOfRangeException(
                nameof(candidate),
                candidate,
                "The KEL-103 limit candidate must identify a supported setpoint.");
        }
    }

    private static ArgumentOutOfRangeException Unsupported(Kel103SetpointLimit limit) =>
        new(
            nameof(limit),
            limit,
            "The KEL-103 setpoint limit is not supported.");
}
