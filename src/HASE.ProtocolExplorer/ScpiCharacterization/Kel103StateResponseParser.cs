using System.Globalization;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal static class Kel103StateResponseParser
{
    private const NumberStyles SupportedNumberStyles =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public static string Parse(
        string response,
        Kel103StateCandidate candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(response);

        return candidate switch
        {
            Kel103StateCandidate.Mode => ParseMode(response),
            Kel103StateCandidate.InputState => ParseInputState(response),
            Kel103StateCandidate.TargetVoltage
                or Kel103StateCandidate.TargetCurrent
                or Kel103StateCandidate.TargetResistance
                or Kel103StateCandidate.TargetPower => ParseSetpoint(response, candidate),
            _ => throw new ArgumentOutOfRangeException(
                nameof(candidate),
                candidate,
                "The KEL-103 state candidate is not supported.")
        };
    }

    private static string ParseMode(string response) =>
        response switch
        {
            "CC" or "CV" or "CR" or "CW" or "SHORt" => response,
            _ => throw InvalidResponse(Kel103StateCandidate.Mode)
        };

    private static string ParseInputState(string response) =>
        response switch
        {
            "OFF" => "Off",
            "ON" => "On",
            _ => throw InvalidResponse(Kel103StateCandidate.InputState)
        };

    private static string ParseSetpoint(
        string response,
        Kel103StateCandidate candidate)
    {
        string unit = candidate.ToUnitSymbol()
            ?? throw new InvalidOperationException("A setpoint candidate must define a unit.");

        if (!response.EndsWith(unit, StringComparison.Ordinal)
            || response.Length == unit.Length)
        {
            throw InvalidResponse(candidate);
        }

        ReadOnlySpan<char> numberText = response.AsSpan(0, response.Length - unit.Length);

        if (!decimal.TryParse(
                numberText,
                SupportedNumberStyles,
                CultureInfo.InvariantCulture,
                out decimal value))
        {
            throw InvalidResponse(candidate);
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static InvalidDataException InvalidResponse(Kel103StateCandidate candidate) =>
        new(
            $"The {candidate.ToArgumentValue()} response does not match the expected KEL-103 format.");
}
