namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Evaluates whether an authenticated client principal may perform one
/// semantic northbound operation.
/// </summary>
public interface IRuntimeHostAuthorizationService
{
    /// <summary>
    /// Evaluates one permission request.
    /// </summary>
    RuntimeHostAuthorizationDecision Authorize(
        RuntimeHostClientPrincipal principal,
        RuntimeHostPermission permission);
}
