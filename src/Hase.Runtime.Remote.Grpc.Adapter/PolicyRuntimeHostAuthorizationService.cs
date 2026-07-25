namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Authorizes exact principal-permission combinations through one immutable
/// default-deny policy.
/// </summary>
public sealed class PolicyRuntimeHostAuthorizationService
    : IRuntimeHostAuthorizationService
{
    /// <summary>
    /// Gets the stable reason returned for an explicit policy grant.
    /// </summary>
    public const string AllowedReason =
        "An explicit authorization policy grants the requested permission.";

    /// <summary>
    /// Gets the stable reason returned when no exact grant exists.
    /// </summary>
    public const string DeniedReason =
        "No explicit authorization policy grants the requested permission.";

    private readonly RuntimeHostAuthorizationPolicy policy;

    /// <summary>
    /// Initializes the policy-backed authorization service.
    /// </summary>
    public PolicyRuntimeHostAuthorizationService(
        RuntimeHostAuthorizationPolicy policy)
    {
        this.policy =
            policy
            ?? throw new ArgumentNullException(
                nameof(policy));
    }

    /// <inheritdoc />
    public RuntimeHostAuthorizationDecision Authorize(
        RuntimeHostClientPrincipal principal,
        RuntimeHostPermission permission)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (string.IsNullOrWhiteSpace(permission.Value))
        {
            throw new ArgumentException(
                "The permission must have a non-empty value.",
                nameof(permission));
        }

        return policy.IsGranted(
            principal.PrincipalId,
            permission)
            ? RuntimeHostAuthorizationDecision.Allow(
                AllowedReason)
            : RuntimeHostAuthorizationDecision.Deny(
                DeniedReason);
    }
}
