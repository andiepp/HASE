namespace Hase.Client;

/// <summary>
/// Identifies one supported normalized remote value kind.
/// </summary>
public enum RemoteValueKind
{
    /// <summary>
    /// No normalized remote value kind has been specified.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// A Boolean value.
    /// </summary>
    Boolean = 1,

    /// <summary>
    /// A string value.
    /// </summary>
    String = 2,

    /// <summary>
    /// A numeric value represented as a double.
    /// </summary>
    Numeric = 3,

    /// <summary>
    /// An opaque ordered sequence of bytes.
    /// </summary>
    ByteArray = 4
}
