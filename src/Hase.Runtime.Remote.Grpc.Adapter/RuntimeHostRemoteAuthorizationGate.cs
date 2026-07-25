namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Composes semantic operation-to-permission mapping with runtime-host
/// authorization evaluation.
/// </summary>
public sealed class RuntimeHostRemoteAuthorizationGate
    : IRuntimeHostRemoteAuthorizationGate
{
    private readonly IRuntimeHostRemoteOperationPermissionMapper
        permissionMapper;
    private readonly IRuntimeHostAuthorizationService
        authorizationService;

    /// <summary>
    /// Initializes the remote authorization gate.
    /// </summary>
    public RuntimeHostRemoteAuthorizationGate(
        IRuntimeHostRemoteOperationPermissionMapper permissionMapper,
        IRuntimeHostAuthorizationService authorizationService)
    {
        this.permissionMapper =
            permissionMapper
            ?? throw new ArgumentNullException(
                nameof(permissionMapper));

        this.authorizationService =
            authorizationService
            ?? throw new ArgumentNullException(
                nameof(authorizationService));
    }

    /// <inheritdoc />
    public RuntimeHostAuthorizationDecision Authorize(
        RuntimeHostClientPrincipal principal,
        RuntimeHostRemoteOperation operation)
    {
        ArgumentNullException.ThrowIfNull(principal);

        RuntimeHostPermission permission =
            permissionMapper.Map(operation);

        return authorizationService.Authorize(
            principal,
            permission);
    }
}
