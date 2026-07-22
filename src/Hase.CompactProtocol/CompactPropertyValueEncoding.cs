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
        0x01
}