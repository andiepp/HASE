namespace Hase.Transport;

/// <summary>
/// Describes the locally observable lifecycle state of a transport connection.
/// </summary>
public enum TransportConnectionState
{
    /// <summary>
    /// The connection is available for transport operations.
    /// </summary>
    Connected,

    /// <summary>
    /// The connection has been closed and can no longer be used.
    /// </summary>
    Closed
}