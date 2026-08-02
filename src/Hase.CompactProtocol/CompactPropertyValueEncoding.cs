namespace Hase.CompactProtocol;

/// <summary>
/// Identifies the descriptor-defined binary representation of one compact
/// property value.
/// </summary>
public enum CompactPropertyValueEncoding : byte
{
    /// <summary>
    /// Encodes a Boolean value as one byte: zero for false and one for true.
    /// </summary>
    Boolean =
        0x01,

    /// <summary>
    /// Encodes a voltage as unsigned 16-bit little-endian millivolts and
    /// materializes it as a <see cref="double"/> value in volts.
    /// </summary>
    Unsigned16LittleEndianMillivolts =
        0x02
}
