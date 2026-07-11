namespace Hase.Protocol;

/// <summary>
/// Identifies the outcome of a protocol operation.
/// Numeric values are part of the wire protocol and must never be changed
/// or reused.
/// </summary>
public enum ProtocolResultCode : byte
{
    /// <summary>
    /// The operation completed successfully.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The request was malformed or semantically invalid.
    /// </summary>
    InvalidRequest = 1,

    /// <summary>
    /// The requested endpoint, instrument, property, command, or event
    /// could not be found.
    /// </summary>
    NotFound = 2,

    /// <summary>
    /// The requested operation is not supported.
    /// </summary>
    NotSupported = 3,

    /// <summary>
    /// The request was understood but deliberately rejected.
    /// </summary>
    Rejected = 4,

    /// <summary>
    /// The operation failed because of an internal endpoint error.
    /// </summary>
    InternalError = 5
}