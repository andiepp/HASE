namespace Hase.Runtime.Northbound;

/// <summary>
/// Describes a normalized northbound Command execution outcome.
/// </summary>
public enum RuntimeHostCommandOperationStatus
{
    /// <summary>
    /// The Command completed successfully.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The addressed attachment is no longer current.
    /// </summary>
    AttachmentNotCurrent = 1,

    /// <summary>
    /// The addressed instrument does not exist.
    /// </summary>
    InstrumentNotFound = 2,

    /// <summary>
    /// The addressed Command does not exist.
    /// </summary>
    CommandNotFound = 3,

    /// <summary>
    /// The supplied argument is not supported by the Command path.
    /// </summary>
    ArgumentNotSupported = 4,

    /// <summary>
    /// The endpoint cannot currently execute the Command.
    /// </summary>
    EndpointUnavailable = 5,

    /// <summary>
    /// The endpoint deliberately rejected the Command.
    /// </summary>
    EndpointRejected = 6,

    /// <summary>
    /// The endpoint reported a Command failure.
    /// </summary>
    EndpointFailure = 7,

    /// <summary>
    /// The Command did not complete within its allowed time.
    /// </summary>
    TimedOut = 8,
}