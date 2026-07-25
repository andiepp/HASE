using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Translates platform certificate-chain evaluation into deterministic HASE
/// trust outcomes.
/// </summary>
public sealed class RuntimeHostCertificateTrustValidator
    : IRuntimeHostCertificateTrustValidator
{
    private readonly IRuntimeHostCertificateTrustEvaluator trustEvaluator;

    /// <summary>
    /// Initializes the trust validator.
    /// </summary>
    public RuntimeHostCertificateTrustValidator(
        IRuntimeHostCertificateTrustEvaluator trustEvaluator)
    {
        this.trustEvaluator =
            trustEvaluator
            ?? throw new ArgumentNullException(
                nameof(trustEvaluator));
    }

    /// <summary>
    /// Creates a validator backed by system X.509 trust.
    /// </summary>
    public static RuntimeHostCertificateTrustValidator CreateSystemTrust()
    {
        return new RuntimeHostCertificateTrustValidator(
            new RuntimeHostSystemCertificateTrustEvaluator());
    }

    /// <inheritdoc />
    public RuntimeHostCertificateTrustValidationResult Validate(
        X509Certificate2? certificate,
        DateTimeOffset validationTimeUtc)
    {
        if (validationTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The certificate-trust validation time must use UTC.",
                nameof(validationTimeUtc));
        }

        if (certificate is null)
        {
            return RuntimeHostCertificateTrustValidationResult.Untrusted(
                RuntimeHostCertificateTrustFailureReason.CertificateMissing);
        }

        try
        {
            bool trusted =
                trustEvaluator.IsTrusted(
                    certificate,
                    validationTimeUtc);

            return trusted
                ? RuntimeHostCertificateTrustValidationResult.Trusted()
                : RuntimeHostCertificateTrustValidationResult.Untrusted(
                    RuntimeHostCertificateTrustFailureReason.ChainNotTrusted);
        }
        catch (CryptographicException)
        {
            return RuntimeHostCertificateTrustValidationResult.Untrusted(
                RuntimeHostCertificateTrustFailureReason
                    .TrustEvaluationFailed);
        }
        catch (InvalidOperationException)
        {
            return RuntimeHostCertificateTrustValidationResult.Untrusted(
                RuntimeHostCertificateTrustFailureReason
                    .TrustEvaluationFailed);
        }
    }
}
