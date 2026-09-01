using System.Globalization;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal static class Kel103MeasurementResponseParser
{
    private const NumberStyles SupportedNumberStyles =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public static decimal Parse(
        string response,
        Kel103MeasurementCandidate candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(response);

        string unit = candidate.ToUnitSymbol();

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

        return value;
    }

    private static InvalidDataException InvalidResponse(Kel103MeasurementCandidate candidate) =>
        new(
            $"The {candidate.ToArgumentValue()} response does not match the expected invariant numeric value and unit.");
}
