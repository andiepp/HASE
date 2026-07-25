namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Authenticates validated northbound client credential identities against
/// explicit HASE credential enrollment.
/// </summary>
public interface IRuntimeHostClientAuthenticationService
{
    /// <summary>
    /// Authenticates one validated credential identity at the supplied UTC
    /// authentication time.
    /// </summary>
    RuntimeHostAuthenticationResult Authenticate(
        RuntimeHostClientCredentialIdentity credentialIdentity,
        DateTimeOffset authenticatedAtUtc);
}
