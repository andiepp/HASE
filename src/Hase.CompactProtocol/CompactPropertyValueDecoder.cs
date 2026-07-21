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

            _ =>
                throw new InvalidOperationException(
                    $"Compact property-value encoding '{encoding}' is not "
                    + "supported.")
        };
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