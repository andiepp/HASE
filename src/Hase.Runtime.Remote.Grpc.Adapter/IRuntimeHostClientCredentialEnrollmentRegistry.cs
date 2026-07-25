namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Resolves enrolled client credentials to stable HASE client principals.
/// </summary>
public interface IRuntimeHostClientCredentialEnrollmentRegistry
{
    /// <summary>
    /// Attempts to resolve one credential identity.
    /// </summary>
    bool TryResolve(
        RuntimeHostClientCredentialIdentity credentialIdentity,
        DateTimeOffset authenticatedAtUtc,
        out RuntimeHostClientPrincipal? principal);
}
