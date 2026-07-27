namespace Hase.Client;

/// <summary>
/// Describes one normalized remote Property query or operation outcome.
/// </summary>
public enum RemotePropertyOperationStatus
{
    /// <summary>
    /// No Property operation status has been specified.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// The query or operation completed successfully.
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
    /// The addressed Property does not exist.
    /// </summary>
    PropertyNotFound = 4,

    /// <summary>
    /// The Property does not support authoritative reads.
    /// </summary>
    ReadNotSupported = 5,

    /// <summary>
    /// The Property does not support writes.
    /// </summary>
    WriteNotSupported = 6,

    /// <summary>
    /// The requested value is invalid for the Property.
    /// </summary>
    InvalidValue = 7,

    /// <summary>
    /// The endpoint cannot currently perform the operation.
    /// </summary>
    EndpointUnavailable = 8,

    /// <summary>
    /// The endpoint deliberately rejected the operation.
    /// </summary>
    EndpointRejected = 9,

    /// <summary>
    /// The endpoint reported an operation failure.
    /// </summary>
    EndpointFailure = 10,

    /// <summary>
    /// The operation did not complete within its allowed time.
    /// </summary>
    TimedOut = 11
}
