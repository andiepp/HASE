using System.Text;

namespace Hase.DesktopHost;

public static class DesktopRuntimeByteFormatting
{
    public static string FormatHex(
        IReadOnlyList<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(
            bytes);

        if (bytes.Count == 0)
        {
            return string.Empty;
        }

        var builder =
            new StringBuilder(
                (bytes.Count * 3) - 1);

        for (int index = 0;
            index < bytes.Count;
            index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.AppendFormat(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:X2}",
                bytes[index]);
        }

        return builder.ToString();
    }
}
