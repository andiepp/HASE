namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Describes the transport-independent outcome of an attachment-bound Command
/// operation.
/// </summary>
public enum EndpointAttachmentCommandOperationStatus
{
    /// <summary>
    /// The Command completed successfully.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The supplied argument is not supported by the attachment's Command
    /// path.
    /// </summary>
    ArgumentNotSupported = 1,

    /// <summary>
    /// The attached endpoint deliberately rejected the Command.
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// The attached endpoint reported a Command failure.
    /// </summary>
    Failure = 3,

    /// <summary>
    /// The attachment cannot currently execute the Command.
    /// </summary>
    Unavailable = 4,

    /// <summary>
    /// The Command did not complete within its allowed time.
    /// </summary>
    TimedOut = 5,
}