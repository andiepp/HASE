using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Translates platform X.509 trust evaluation into the HASE
/// transport-independent trust result model.
/// </summary>
public interface IRuntimeHostCertificateTrustValidator
{
    /// <summary>
    /// Validates certificate-chain trust at the explicit UTC evaluation time.
    /// </summary>
    RuntimeHostCertificateTrustValidationResult Validate(
        X509Certificate2? certificate,
        DateTimeOffset validationTimeUtc);
}
