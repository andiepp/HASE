using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Applies deterministic local validation rules to one presented X.509 client
/// certificate.
/// </summary>
public interface IRuntimeHostClientCertificateValidator
{
    /// <summary>
    /// Validates the supplied certificate at the explicit UTC evaluation time.
    /// </summary>
    RuntimeHostClientCertificateValidationResult Validate(
        X509Certificate2? certificate,
        DateTimeOffset validationTimeUtc);
}
