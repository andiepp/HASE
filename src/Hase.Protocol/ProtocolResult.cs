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
}