namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Describes the transport-independent outcome of an attachment-bound
/// Property operation.
/// </summary>
public enum EndpointAttachmentPropertyOperationStatus
{
    /// <summary>
    /// The operation completed with an endpoint-confirmed value.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The attached endpoint does not support the requested operation.
    /// </summary>
    NotSupported = 1,

    /// <summary>
    /// The requested value is invalid for the Property.
    /// </summary>
    InvalidValue = 2,

    /// <summary>
    /// The attached endpoint deliberately rejected the operation.
    /// </summary>
    Rejected = 3,

    /// <summary>
    /// The attached endpoint reported an operation failure.
    /// </summary>
    Failure = 4,

    /// <summary>
    /// The attachment cannot currently perform the operation.
    /// </summary>
    Unavailable = 5,

    /// <summary>
    /// The operation did not complete within its allowed time.
    /// </summary>
    TimedOut = 6,
}