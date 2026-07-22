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

            _ =>
                throw new InvalidOperationException(
                    $"Compact property-value encoding '{encoding}' is not "
                    + "supported.")
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