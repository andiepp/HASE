using System.Globalization;
using System.Text.RegularExpressions;

namespace Hase.Transport.Discovery;

/// <summary>
/// Converts selected Win32_PnPEntity values into a raw Windows USB
/// serial-device record.
/// </summary>
internal static partial class WindowsUsbSerialDeviceRecordParser
{
    internal static WindowsUsbSerialDeviceRecord? Parse(
        string? name,
        string? manufacturer,
        string? pnpDeviceId,
        string? description)
    {
        string? portName =
            ParsePortName(
                name);

        if (portName is null)
        {
            return null;
        }

        return new WindowsUsbSerialDeviceRecord(
            portName,
            ParseHexIdentifier(
                pnpDeviceId,
                VendorIdRegex()),
            ParseHexIdentifier(
                pnpDeviceId,
                ProductIdRegex()),
            NormalizeOptionalText(
                description),
            NormalizeOptionalText(
                manufacturer),
            ParseUsbSerialNumber(
                pnpDeviceId));
    }

    private static string? ParsePortName(
        string? name)
    {
        if (string.IsNullOrWhiteSpace(
            name))
        {
            return null;
        }

        Match match =
            PortNameRegex().Match(
                name);

        if (!match.Success)
        {
            return null;
        }

        return match
            .Groups["port"]
            .Value
            .ToUpperInvariant();
    }

    private static ushort? ParseHexIdentifier(
        string? pnpDeviceId,
        Regex regex)
    {
        if (string.IsNullOrWhiteSpace(
            pnpDeviceId))
        {
            return null;
        }

        Match match =
            regex.Match(
                pnpDeviceId);

        if (!match.Success)
        {
            return null;
        }

        bool parsed =
            ushort.TryParse(
                match.Groups["value"].Value,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out ushort value);

        return parsed
            ? value
            : null;
    }

    private static string? ParseUsbSerialNumber(
        string? pnpDeviceId)
    {
        if (string.IsNullOrWhiteSpace(
            pnpDeviceId)
            || !pnpDeviceId.StartsWith(
                "USB\\",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string[] segments =
            pnpDeviceId.Split(
                '\\');

        if (segments.Length != 3)
        {
            return null;
        }

        string instanceSegment =
            segments[2].Trim();

        if (instanceSegment.Length == 0
            || instanceSegment.Contains(
                '&',
                StringComparison.Ordinal))
        {
            return null;
        }

        return instanceSegment;
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
            value))
        {
            return null;
        }

        return value.Trim();
    }

    [GeneratedRegex(
        @"\((?<port>COM[0-9]+)\)\s*$",
        RegexOptions.IgnoreCase
        | RegexOptions.CultureInvariant)]
    private static partial Regex PortNameRegex();

    [GeneratedRegex(
        @"(?:^|[\\&])VID_(?<value>[0-9A-F]{4})(?:&|\\|$)",
        RegexOptions.IgnoreCase
        | RegexOptions.CultureInvariant)]
    private static partial Regex VendorIdRegex();

    [GeneratedRegex(
        @"(?:^|[\\&])PID_(?<value>[0-9A-F]{4})(?:&|\\|$)",
        RegexOptions.IgnoreCase
        | RegexOptions.CultureInvariant)]
    private static partial Regex ProductIdRegex();
}