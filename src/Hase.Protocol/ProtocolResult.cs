namespace Hase.Protocol;

/// <summary>
/// Describes the outcome of a protocol operation.
/// </summary>
public readonly record struct ProtocolResult(
    ProtocolResultCode Code,
    string? Message)
{
    /// <summary>
    /// Gets a successful protocol result without a diagnostic message.
    /// </summary>
    public static ProtocolResult Success { get; } =
        new(ProtocolResultCode.Success, null);

    /// <summary>
    /// Gets whether the operation completed successfully.
    /// </summary>
    public bool IsSuccess =>
        Code == ProtocolResultCode.Success;

    public static ProtocolResult InvalidRequest { get; } =
    new(ProtocolResultCode.InvalidRequest, null);

    public static ProtocolResult NotFound { get; } =
        new(ProtocolResultCode.NotFound, null);

    public static ProtocolResult NotSupported { get; } =
        new(ProtocolResultCode.NotSupported, null);

    public static ProtocolResult Rejected { get; } =
        new(ProtocolResultCode.Rejected, null);

    public static ProtocolResult InternalError { get; } =
        new(ProtocolResultCode.InternalError, null);

}