namespace Hase.Client;

/// <summary>
/// Identifies the normalized connection state of one physical endpoint
/// attachment observed through a remote runtime host.
/// </summary>
public enum RemoteEndpointConnectionState
{
    /// <summary>
    /// No remote endpoint connection state has been specified.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// No active physical endpoint connection exists.
    /// </summary>
    Disconnected = 1,

    /// <summary>
    /// An initial physical endpoint connection attempt is in progress.
    /// </summary>
    Connecting = 2,

    /// <summary>
    /// The physical endpoint state is being synchronized.
    /// </summary>
    Synchronizing = 3,

    /// <summary>
    /// The physical endpoint is connected, synchronized, and ready.
    /// </summary>
    Ready = 4,

    /// <summary>
    /// Recovery of a previously connected physical endpoint is in progress.
    /// </summary>
    Reconnecting = 5,

    /// <summary>
    /// The physical endpoint connection is faulted.
    /// </summary>
    Faulted = 6
}
