namespace Hase.Scpi.Kel103;

/// <summary>
/// Defines and parses the physically characterized read-only identity query.
/// </summary>
public static class Kel103IdentityQuery
{
    public const string CommandText = "*IDN?";

    private const int MaximumResponseCharacters = 511;
    private const string VendorToken = "RND";
    private const string ModelToken = "320-KEL103";
    private const string SerialPrefix = "SN:";

    public static Kel103Identity ParseResponse(string response)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(response);

        if (response.Length > MaximumResponseCharacters
            || response.Any(character => character is < ' ' or > '~'))
        {
            throw InvalidResponse();
        }

        string[] tokens = response.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length != 4
            || !string.Equals(tokens[0], VendorToken, StringComparison.Ordinal)
            || !string.Equals(tokens[1], ModelToken, StringComparison.Ordinal)
            || !IsFirmwareToken(tokens[2])
            || !IsSerialToken(tokens[3]))
        {
            throw InvalidResponse();
        }

        return new Kel103Identity(
            Kel103IdentityDefinition.ProductIdentity,
            tokens[2]);
    }

    private static bool IsFirmwareToken(string token)
    {
        if (token.Length < 4 || token[0] != 'V')
        {
            return false;
        }

        ReadOnlySpan<char> version = token.AsSpan(1);
        var separatorIndex = version.IndexOf('.');

        return separatorIndex > 0
            && separatorIndex < version.Length - 1
            && version[(separatorIndex + 1)..].IndexOf('.') < 0
            && version[..separatorIndex].ToArray().All(char.IsAsciiDigit)
            && version[(separatorIndex + 1)..].ToArray().All(char.IsAsciiDigit);
    }

    private static bool IsSerialToken(string token) =>
        token.StartsWith(SerialPrefix, StringComparison.Ordinal)
        && token.Length > SerialPrefix.Length
        && token.AsSpan(SerialPrefix.Length).ToArray().All(char.IsAsciiLetterOrDigit);

    private static InvalidDataException InvalidResponse() =>
        new("The identification response does not match the supported KEL-103 identity format.");
}
