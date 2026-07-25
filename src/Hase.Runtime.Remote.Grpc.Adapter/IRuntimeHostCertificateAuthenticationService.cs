using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Authenticates one presented X.509 client certificate through the complete
/// HASE certificate-authentication pipeline.
/// </summary>
public interface IRuntimeHostCertificateAuthenticationService
{
    /// <summary>
    /// Validates and authenticates the supplied certificate at the explicit
    /// UTC authentication time.
    /// </summary>
    RuntimeHostCertificateAuthenticationResult Authenticate(
        X509Certificate2? certificate,
        DateTimeOffset authenticatedAtUtc);
}
