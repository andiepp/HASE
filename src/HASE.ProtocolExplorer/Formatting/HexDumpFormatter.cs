using System.Text;

namespace Hase.ProtocolExplorer.Formatting;

internal sealed class HexDumpFormatter
{
    private const int BytesPerLine = 16;

    public IReadOnlyList<string> Format(
        ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return
            [
                "<empty>"
            ];
        }

        List<string> lines = [];

        for (int offset = 0;
            offset < bytes.Length;
            offset += BytesPerLine)
        {
            int count =
                Math.Min(
                    BytesPerLine,
                    bytes.Length - offset);

            ReadOnlySpan<byte> lineBytes =
                bytes.Slice(
                    offset,
                    count);

            lines.Add(
                FormatLine(
                    offset,
                    lineBytes));
        }

        return lines;
    }

    private static string FormatLine(
        int offset,
        ReadOnlySpan<byte> bytes)
    {
        StringBuilder builder =
            new();

        builder.Append(
            offset.ToString("X4"));

        builder.Append("  ");

        for (int index = 0;
            index < BytesPerLine;
            index++)
        {
            if (index < bytes.Length)
            {
                builder.Append(
                    bytes[index].ToString("X2"));
            }
            else
            {
                builder.Append("  ");
            }

            if (index < BytesPerLine - 1)
            {
                builder.Append(' ');
            }
        }

        builder.Append("  ");

        foreach (byte value in bytes)
        {
            char character =
                value is >= 32 and <= 126
                    ? (char)value
                    : '.';

            builder.Append(character);
        }

        return builder.ToString();
    }
}