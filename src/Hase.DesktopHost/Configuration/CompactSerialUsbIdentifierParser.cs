using System.Globalization;

namespace Hase.DesktopHost.Configuration;

public static class CompactSerialUsbIdentifierParser
{
    public static ushort ParseExactHex16(string value, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        if (value.Length != 6
            || !value.StartsWith("0x", StringComparison.Ordinal)
            || !ushort.TryParse(value.AsSpan(2), NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture, out ushort parsed))
        {
            throw new ArgumentException($"The {role} must use exact 0xNNNN hexadecimal form.", nameof(value));
        }

        return parsed;
    }
}
