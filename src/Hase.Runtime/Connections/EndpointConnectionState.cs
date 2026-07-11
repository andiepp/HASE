namespace Hase.Runtime.Connections;

/// <summary>
/// Describes the connection lifecycle state of a runtime endpoint.
/// </summary>
public enum EndpointConnectionState
{
    /// <summary>
    /// No active connection exists.
    /// </summary>
    Disconnected,

    /// <summary>
    /// An initial connection attempt is in progress.
    /// </summary>
    Connecting,

    /// <summary>
    /// The transport connection exists and the endpoint state
    /// is being synchronized with the physical device.
    /// </summary>
    Synchronizing,

    /// <summary>
    /// The endpoint is connected, synchronized, and ready for use.
    /// </summary>
    Ready,

    /// <summary>
    /// The endpoint was previously connected and a new connection
    /// attempt is in progress.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// The connection lifecycle encountered an unrecoverable error
    /// or has temporarily stopped retrying.
    /// </summary>
    Faulted
}