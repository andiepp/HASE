namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Represents the deterministic result of authenticating one validated
/// northbound client credential.
/// </summary>
public sealed record RuntimeHostAuthenticationResult
{
    private RuntimeHostAuthenticationResult(
        RuntimeHostClientPrincipal? principal,
        RuntimeHostAuthenticationFailureReason failureReason)
    {
        bool authenticated = principal is not null;
        bool failed =
            failureReason
            != RuntimeHostAuthenticationFailureReason.None;

        if (authenticated == failed)
        {
            throw new ArgumentException(
                "An authentication result must contain either one "
                + "authenticated principal or one failure reason.");
        }

        Principal = principal;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets a value indicating whether authentication succeeded.
    /// </summary>
    public bool IsAuthenticated =>
        Principal is not null;

    /// <summary>
    /// Gets the authenticated principal, or null when authentication failed.
    /// </summary>
    public RuntimeHostClientPrincipal? Principal { get; }

    /// <summary>
    /// Gets the failure reason, or None when authentication succeeded.
    /// </summary>
    public RuntimeHostAuthenticationFailureReason FailureReason { get; }

    /// <summary>
    /// Creates one successful authentication result.
    /// </summary>
    public static RuntimeHostAuthenticationResult Authenticated(
        RuntimeHostClientPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(
            principal);

        return new RuntimeHostAuthenticationResult(
            principal,
            RuntimeHostAuthenticationFailureReason.None);
    }

    /// <summary>
    /// Creates one failed authentication result.
    /// </summary>
    public static RuntimeHostAuthenticationResult Failed(
        RuntimeHostAuthenticationFailureReason failureReason)
    {
        if (failureReason
            == RuntimeHostAuthenticationFailureReason.None)
        {
            throw new ArgumentException(
                "An authentication failure reason must be specified.",
                nameof(failureReason));
        }

        if (!Enum.IsDefined(
            failureReason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureReason));
        }

        return new RuntimeHostAuthenticationResult(
            null,
            failureReason);
    }
}
