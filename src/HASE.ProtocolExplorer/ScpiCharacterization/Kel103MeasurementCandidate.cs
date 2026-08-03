namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal enum Kel103MeasurementCandidate
{
    Voltage = 0,
    Current = 1,
    Power = 2
}

internal static class Kel103MeasurementCandidateExtensions
{
    public static string ToArgumentValue(this Kel103MeasurementCandidate candidate) =>
        candidate switch
        {
            Kel103MeasurementCandidate.Voltage => "voltage",
            Kel103MeasurementCandidate.Current => "current",
            Kel103MeasurementCandidate.Power => "power",
            _ => throw Unsupported(candidate)
        };

    public static string ToQueryText(this Kel103MeasurementCandidate candidate) =>
        candidate switch
        {
            Kel103MeasurementCandidate.Voltage => ":MEASure:VOLTage?",
            Kel103MeasurementCandidate.Current => ":MEASure:CURRent?",
            Kel103MeasurementCandidate.Power => ":MEASure:POWer?",
            _ => throw Unsupported(candidate)
        };

    public static string ToUnitSymbol(this Kel103MeasurementCandidate candidate) =>
        candidate switch
        {
            Kel103MeasurementCandidate.Voltage => "V",
            Kel103MeasurementCandidate.Current => "A",
            Kel103MeasurementCandidate.Power => "W",
            _ => throw Unsupported(candidate)
        };

    private static ArgumentOutOfRangeException Unsupported(Kel103MeasurementCandidate candidate) =>
        new(
            nameof(candidate),
            candidate,
            "The KEL-103 measurement candidate is not supported.");
}
