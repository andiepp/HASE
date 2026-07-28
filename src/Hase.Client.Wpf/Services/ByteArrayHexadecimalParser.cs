using System.Globalization;
using Hase.Core.Domain.Data;

namespace Hase.Client.Wpf.Services;

/// <summary>
/// Parses ByteArray command arguments from hexadecimal user input.
/// Whitespace may separate bytes and has no semantic meaning.
/// </summary>
public static class ByteArrayHexadecimalParser
{
    /// <summary>
    /// Attempts to parse at least one complete hexadecimal byte.
    /// </summary>
    public static bool TryParse(
        string? text,
        out ByteArrayValue? value)
    {
        value =
            null;

        if (string.IsNullOrWhiteSpace(
                text))
        {
            return false;
        }

        string hexadecimal =
            string.Concat(
                text.Where(
                    character =>
                        !char.IsWhiteSpace(
                            character)));

        if (hexadecimal.Length == 0
            || hexadecimal.Length % 2 != 0)
        {
            return false;
        }

        var bytes =
            new byte[
                hexadecimal.Length / 2];

        for (int index = 0; index < bytes.Length; index++)
        {
            if (!byte.TryParse(
                    hexadecimal.AsSpan(
                        index * 2,
                        2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out bytes[index]))
            {
                return false;
            }
        }

        value =
            new ByteArrayValue(
                bytes);

        return true;
    }
}
