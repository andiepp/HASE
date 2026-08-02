namespace Hase.CompactProtocol;

/// <summary>
/// Decodes compact endpoint value bytes according to the encoding selected by
/// the host-side compact property mapping.
/// </summary>
internal static class CompactPropertyValueDecoder
{
    public static object Decode(
        CompactPropertyValueEncoding encoding,
        ReadOnlySpan<byte> value)
    {
        if (!Enum.IsDefined(
                encoding))
        {
            throw new ArgumentOutOfRangeException(
                nameof(encoding),
                encoding,
                "The compact property-value encoding is not defined.");
        }

        return encoding switch
        {
            CompactPropertyValueEncoding.Boolean =>
                DecodeBoolean(
                    value),

            CompactPropertyValueEncoding.Unsigned16LittleEndianMillivolts =>
                DecodeUnsigned16LittleEndianMillivolts(
                    value),

            _ =>
                throw new InvalidOperationException(
                    $"Compact property-value encoding '{encoding}' is not "
                    + "supported.")
        };
    }

    private static double DecodeUnsigned16LittleEndianMillivolts(
        ReadOnlySpan<byte> value)
    {
        if (value.Length != 2)
        {
            throw new InvalidDataException(
                "An unsigned 16-bit little-endian millivolt value must "
                + "contain exactly two bytes.");
        }

        ushort millivolts =
            (ushort)(
                value[0]
                | value[1] << 8);

        return millivolts / 1000.0;
    }

    private static bool DecodeBoolean(
        ReadOnlySpan<byte> value)
    {
        if (value.Length != 1)
        {
            throw new InvalidDataException(
                "A compact Boolean property value must contain exactly "
                + "one byte.");
        }

        return value[0] switch
        {
            0x00 =>
                false,

            0x01 =>
                true,

            _ =>
                throw new InvalidDataException(
                    $"Compact Boolean value 0x{value[0]:X2} is not valid.")
        };
    }
}
