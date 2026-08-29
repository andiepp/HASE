namespace Hase.CompactProtocol;

/// <summary>
/// Encodes property values according to the encoding selected by the host-side
/// compact property mapping.
/// </summary>
internal static class CompactPropertyValueEncoder
{
    public static ReadOnlyMemory<byte> Encode(
        CompactPropertyValueEncoding encoding,
        object value)
    {
        if (!Enum.IsDefined(
                encoding))
        {
            throw new ArgumentOutOfRangeException(
                nameof(encoding),
                encoding,
                "The compact property-value encoding is not defined.");
        }

        ArgumentNullException.ThrowIfNull(
            value);

        return encoding switch
        {
            CompactPropertyValueEncoding.Boolean =>
                EncodeBoolean(
                    value),

            CompactPropertyValueEncoding.Unsigned16LittleEndianMillivolts =>
                EncodeUnsigned16LittleEndianMillivolts(
                    value),

            CompactPropertyValueEncoding.Unsigned16LittleEndian =>
                EncodeUnsigned16LittleEndian(
                    value),

            _ =>
                throw new InvalidOperationException(
                    $"Compact property-value encoding '{encoding}' is not "
                    + "supported.")
        };
    }

    private static ReadOnlyMemory<byte>
        EncodeUnsigned16LittleEndianMillivolts(
            object value)
    {
        if (value is not double volts)
        {
            throw new ArgumentException(
                "A compact millivolt value must be represented by "
                + "System.Double volts.",
                nameof(value));
        }

        if (!double.IsFinite(volts)
            || volts < 0.0
            || volts > ushort.MaxValue / 1000.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A compact millivolt value must be finite and encode within "
                + "an unsigned 16-bit value.");
        }

        ushort millivolts =
            checked((ushort)Math.Round(
                volts * 1000.0,
                MidpointRounding.AwayFromZero));

        return new byte[]
        {
            (byte)(millivolts & 0xFF),
            (byte)(millivolts >> 8)
        };
    }

    private static ReadOnlyMemory<byte> EncodeUnsigned16LittleEndian(
        object value)
    {
        if (value is not double numericValue)
        {
            throw new ArgumentException(
                "A compact unsigned 16-bit value must be represented by "
                + "System.Double.",
                nameof(value));
        }

        if (!double.IsFinite(numericValue)
            || numericValue < 0.0
            || numericValue > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A compact unsigned 16-bit value must be finite and encode "
                + "within an unsigned 16-bit value.");
        }

        ushort rawValue =
            checked((ushort)Math.Round(
                numericValue,
                MidpointRounding.AwayFromZero));

        return new byte[]
        {
            (byte)(rawValue & 0xFF),
            (byte)(rawValue >> 8)
        };
    }

    private static ReadOnlyMemory<byte> EncodeBoolean(
        object value)
    {
        if (value is not bool booleanValue)
        {
            throw new ArgumentException(
                "A compact Boolean property value must be represented by "
                + "System.Boolean.",
                nameof(value));
        }

        return new byte[]
        {
            booleanValue
                ? (byte)0x01
                : (byte)0x00
        };
    }
}
