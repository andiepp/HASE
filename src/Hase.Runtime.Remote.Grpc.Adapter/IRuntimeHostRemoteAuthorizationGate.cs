namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Authorizes one authenticated client principal for one semantic northbound
/// remote operation.
/// </summary>
public interface IRuntimeHostRemoteAuthorizationGate
{
    /// <summary>
    /// Maps the operation to its required permission and evaluates the
    /// authorization policy.
    /// </summary>
    RuntimeHostAuthorizationDecision Authorize(
        RuntimeHostClientPrincipal principal,
        RuntimeHostRemoteOperation operation);
}
