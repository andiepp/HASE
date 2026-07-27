namespace Hase.Client;

/// <summary>
/// Describes one normalized remote Command execution outcome.
/// </summary>
public enum RemoteCommandOperationStatus
{
    /// <summary>
    /// No Command operation status has been specified.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// The Command completed successfully.
    /// </summary>
    Success = 1,

    /// <summary>
    /// The addressed attachment is no longer current.
    /// </summary>
    AttachmentNotCurrent = 2,

    /// <summary>
    /// The addressed instrument does not exist.
    /// </summary>
    InstrumentNotFound = 3,

    /// <summary>
    /// The addressed Command does not exist.
    /// </summary>
    CommandNotFound = 4,

    /// <summary>
    /// The supplied argument is not supported by the Command.
    /// </summary>
    ArgumentNotSupported = 5,

    /// <summary>
    /// The endpoint cannot currently execute the Command.
    /// </summary>
    EndpointUnavailable = 6,

    /// <summary>
    /// The endpoint deliberately rejected the Command.
    /// </summary>
    EndpointRejected = 7,

    /// <summary>
    /// The endpoint reported a Command failure.
    /// </summary>
    EndpointFailure = 8,

    /// <summary>
    /// The Command did not complete within its allowed time.
    /// </summary>
    TimedOut = 9
}
