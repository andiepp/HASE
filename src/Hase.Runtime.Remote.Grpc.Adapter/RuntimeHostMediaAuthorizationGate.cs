namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Requires every permission assigned to one media-control operation.
/// </summary>
public sealed class RuntimeHostMediaAuthorizationGate
{
    private readonly IRuntimeHostAuthorizationService authorizationService;

    public RuntimeHostMediaAuthorizationGate(
        IRuntimeHostAuthorizationService authorizationService)
    {
        this.authorizationService = authorizationService ??
            throw new ArgumentNullException(nameof(authorizationService));
    }

    public RuntimeHostAuthorizationDecision Authorize(
        RuntimeHostClientPrincipal principal,
        IReadOnlyList<RuntimeHostPermission> requirements)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(requirements);
        if (requirements.Count == 0)
        {
            throw new ArgumentException(
                "At least one media permission is required.",
                nameof(requirements));
        }

        foreach (RuntimeHostPermission permission in requirements)
        {
            RuntimeHostAuthorizationDecision decision =
                authorizationService.Authorize(principal, permission) ??
                throw new InvalidOperationException(
                    "The authorization service returned no decision.");
            if (!decision.IsAllowed)
            {
                return RuntimeHostAuthorizationDecision.Deny(
                    "required-media-permission-denied");
            }
        }

        return RuntimeHostAuthorizationDecision.Allow(
            "required-media-permissions-granted");
    }
}
