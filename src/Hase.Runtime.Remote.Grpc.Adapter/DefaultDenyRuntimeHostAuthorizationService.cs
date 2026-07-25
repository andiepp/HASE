namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Provides the fail-closed authorization baseline used when no explicit
/// permission policy has granted an operation.
/// </summary>
public sealed class DefaultDenyRuntimeHostAuthorizationService
    : IRuntimeHostAuthorizationService
{
    /// <summary>
    /// Gets the stable reason returned by the default-deny service.
    /// </summary>
    public const string DefaultDenialReason =
        "No explicit authorization policy grants the requested permission.";

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

        return RuntimeHostAuthorizationDecision.Deny(
            DefaultDenialReason);
    }
}
