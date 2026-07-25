namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Represents one immutable northbound authorization decision.
/// </summary>
public sealed record RuntimeHostAuthorizationDecision
{
    private RuntimeHostAuthorizationDecision(
        bool isAllowed,
        string reason)
    {
        IsAllowed = isAllowed;
        Reason = reason;
    }

    /// <summary>
    /// Gets a value indicating whether the requested operation is authorized.
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// Gets the stable non-sensitive reason for the decision.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Creates an allowed decision.
    /// </summary>
    public static RuntimeHostAuthorizationDecision Allow(
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            reason,
            nameof(reason));

        return new RuntimeHostAuthorizationDecision(
            true,
            reason);
    }

    /// <summary>
    /// Creates a denied decision.
    /// </summary>
    public static RuntimeHostAuthorizationDecision Deny(
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            reason,
            nameof(reason));

        return new RuntimeHostAuthorizationDecision(
            false,
            reason);
    }
}
