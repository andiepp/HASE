namespace Hase.Transport.Serial;

/// <summary>
/// Defines the flow-control mechanism used by a serial transport.
/// </summary>
public enum SerialHandshake
{
    /// <summary>
    /// No flow control is used.
    /// </summary>
    None = 0,

    /// <summary>
    /// Software XON/XOFF flow control is used.
    /// </summary>
    XOnXOff = 1,

    /// <summary>
    /// Request-to-send hardware flow control is used.
    /// </summary>
    RequestToSend = 2,

    /// <summary>
    /// Request-to-send hardware and XON/XOFF software flow control are used.
    /// </summary>
    RequestToSendXOnXOff = 3
}