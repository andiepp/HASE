namespace Hase.CompactProtocol;

/// <summary>
/// Identifies the descriptor-defined binary representation of one compact
/// event value.
/// </summary>
public enum CompactEventValueEncoding : byte
{
    /// <summary>
    /// Indicates that the event carries no value bytes.
    /// </summary>
    None =
        0x00
}