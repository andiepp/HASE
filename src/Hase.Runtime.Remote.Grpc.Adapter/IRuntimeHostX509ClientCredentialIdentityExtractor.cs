using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Extracts a transport-independent HASE client credential identity from an
/// already validated X.509 client certificate.
/// </summary>
public interface IRuntimeHostX509ClientCredentialIdentityExtractor
{
    /// <summary>
    /// Extracts the deterministic credential identity for the supplied
    /// already validated client certificate.
    /// </summary>
    RuntimeHostClientCredentialIdentity Extract(
        X509Certificate2 certificate);
}
