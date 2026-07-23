namespace Hase.Runtime.Northbound;

/// <summary>
/// Describes a normalized northbound Property query or operation outcome.
/// </summary>
public enum RuntimeHostPropertyOperationStatus
{
    /// <summary>
    /// The query or operation completed successfully.
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
    /// The addressed Property does not exist.
    /// </summary>
    PropertyNotFound = 3,

    /// <summary>
    /// The Property does not support authoritative reads.
    /// </summary>
    ReadNotSupported = 4,

    /// <summary>
    /// The Property does not support writes.
    /// </summary>
    WriteNotSupported = 5,

    /// <summary>
    /// The requested value is invalid for the Property.
    /// </summary>
    InvalidValue = 6,

    /// <summary>
    /// The endpoint cannot currently perform the operation.
    /// </summary>
    EndpointUnavailable = 7,

    /// <summary>
    /// The endpoint deliberately rejected the operation.
    /// </summary>
    EndpointRejected = 8,

    /// <summary>
    /// The endpoint reported an operation failure.
    /// </summary>
    EndpointFailure = 9,

    /// <summary>
    /// The operation did not complete within its allowed time.
    /// </summary>
    TimedOut = 10,
}
