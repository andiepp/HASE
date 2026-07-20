namespace Hase.Transport.Serial;

/// <summary>
/// Defines the number of stop bits used by a serial transport.
/// </summary>
public enum SerialStopBits
{
    /// <summary>
    /// One stop bit is used.
    /// </summary>
    One = 0,

    /// <summary>
    /// One and one-half stop bits are used.
    /// </summary>
    OnePointFive = 1,

    /// <summary>
    /// Two stop bits are used.
    /// </summary>
    Two = 2
}