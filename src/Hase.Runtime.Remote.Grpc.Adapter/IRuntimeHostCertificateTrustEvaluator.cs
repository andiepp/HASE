using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Evaluates one X.509 certificate against the configured platform trust
/// policy.
/// </summary>
public interface IRuntimeHostCertificateTrustEvaluator
{
    /// <summary>
    /// Returns true only when the platform builds a trusted certificate chain.
    /// </summary>
    bool IsTrusted(
        X509Certificate2 certificate,
        DateTimeOffset validationTimeUtc);
}
