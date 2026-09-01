namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed record Kel103UnrecognizedStateResponseObservation(
    int ResponseCharacterCount,
    string? ObservedToken,
    bool HasLeadingSign,
    int IntegerDigitCount,
    string DecimalSeparator,
    int FractionalDigitCount,
    string Suffix,
    bool ContainsWhitespace,
    bool ContainsUnexpectedCharacters)
{
    public static Kel103UnrecognizedStateResponseObservation Create(
        string response,
        Kel103StateCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (candidate is Kel103StateCandidate.Mode or Kel103StateCandidate.InputState)
        {
            bool printable = response.All(character => character is >= '!' and <= '~');

            return new Kel103UnrecognizedStateResponseObservation(
                response.Length,
                printable ? response : null,
                false,
                0,
                "None",
                0,
                "None",
                response.Any(char.IsWhiteSpace),
                !printable);
        }

        return CreateSetpointShape(response);
    }

    private static Kel103UnrecognizedStateResponseObservation CreateSetpointShape(
        string response)
    {
        int index = 0;
        bool hasLeadingSign = response.Length > 0 && response[0] is '+' or '-';
        if (hasLeadingSign)
        {
            index++;
        }

        int integerStart = index;
        while (index < response.Length && char.IsAsciiDigit(response[index]))
        {
            index++;
        }

        int integerDigitCount = index - integerStart;
        string decimalSeparator = "None";
        int fractionalDigitCount = 0;

        if (index < response.Length && response[index] is '.' or ',')
        {
            decimalSeparator = response[index].ToString();
            index++;
            int fractionalStart = index;
            while (index < response.Length && char.IsAsciiDigit(response[index]))
            {
                index++;
            }

            fractionalDigitCount = index - fractionalStart;
        }

        string suffixText = response[index..];
        bool suffixPrintable = suffixText.All(character => character is >= '!' and <= '~');
        bool containsWhitespace = response.Any(char.IsWhiteSpace);
        bool containsUnexpectedCharacters =
            integerDigitCount == 0
            || (decimalSeparator != "None" && fractionalDigitCount == 0)
            || !suffixPrintable;

        return new Kel103UnrecognizedStateResponseObservation(
            response.Length,
            null,
            hasLeadingSign,
            integerDigitCount,
            decimalSeparator,
            fractionalDigitCount,
            suffixPrintable && suffixText.Length > 0 ? suffixText : "None",
            containsWhitespace,
            containsUnexpectedCharacters);
    }
}
