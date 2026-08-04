using System.Globalization;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed record Kel103SetpointChangeCandidate(
    string OriginalValue,
    string ChangedValue)
{
    public static Kel103SetpointChangeCandidate Create(
        string originalValue,
        string lowerBound,
        string upperBound)
    {
        decimal original = Parse(originalValue, nameof(originalValue));
        decimal lower = Parse(lowerBound, nameof(lowerBound));
        decimal upper = Parse(upperBound, nameof(upperBound));

        if (lower >= upper || original < lower || original > upper)
        {
            throw new InvalidDataException(
                "The authoritative setpoint and bounds cannot define a safe characterization candidate.");
        }

        int scale = FractionalDigitCount(originalValue);
        decimal quantum = 1m;
        for (int index = 0; index < scale; index++)
        {
            quantum /= 10m;
        }

        decimal increased = original + quantum;
        decimal decreased = original - quantum;
        decimal changed;

        if (increased > lower && increased < upper)
        {
            changed = increased;
        }
        else if (decreased > lower && decreased < upper)
        {
            changed = decreased;
        }
        else
        {
            throw new InvalidDataException(
                "The characterized bounds contain no different interior response-scale candidate.");
        }

        string changedValue = changed.ToString($"F{scale}", CultureInfo.InvariantCulture);
        if (string.Equals(changedValue, originalValue, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The derived characterization candidate is not different from the authoritative setpoint.");
        }

        return new Kel103SetpointChangeCandidate(originalValue, changedValue);
    }

    private static decimal Parse(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        if (!decimal.TryParse(
                value,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal parsed))
        {
            throw new InvalidDataException(
                "A normalized KEL-103 setpoint or bound is not an invariant decimal value.");
        }

        return parsed;
    }

    private static int FractionalDigitCount(string value)
    {
        int separatorIndex = value.IndexOf('.');
        return separatorIndex < 0 ? 0 : value.Length - separatorIndex - 1;
    }
}
