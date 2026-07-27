namespace Hase.Client;

/// <summary>
/// Identifies the normalized lifecycle state of one client session with a
/// remote runtime host.
/// </summary>
/// <remarks>
/// This state is independent of the connection state of every physical
/// endpoint published by the runtime host.
/// </remarks>
public enum RuntimeHostClientSessionState
{
    /// <summary>
    /// No client session state has been specified.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// No remote runtime-host session is active.
    /// </summary>
    Disconnected = 1,

    /// <summary>
    /// Initial remote-session establishment is in progress.
    /// </summary>
    Connecting = 2,

    /// <summary>
    /// The remote session has an authoritative initial snapshot.
    /// </summary>
    Connected = 3,

    /// <summary>
    /// Recovery of a previously connected remote session is in progress.
    /// </summary>
    Reconnecting = 4,

    /// <summary>
    /// Orderly remote-session shutdown is in progress.
    /// </summary>
    Disconnecting = 5,

    /// <summary>
    /// Remote-session establishment or operation has faulted.
    /// </summary>
    Faulted = 6
}
