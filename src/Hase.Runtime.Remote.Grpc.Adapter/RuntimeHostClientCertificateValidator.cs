using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Applies certificate-presence, validity-interval, and client-authentication
/// Enhanced Key Usage rules without performing chain or revocation validation.
/// </summary>
public sealed class RuntimeHostClientCertificateValidator
    : IRuntimeHostClientCertificateValidator
{
    private const string EnhancedKeyUsageExtensionOid =
        "2.5.29.37";

    private const string ClientAuthenticationUsageOid =
        "1.3.6.1.5.5.7.3.2";

    /// <inheritdoc />
    public RuntimeHostClientCertificateValidationResult Validate(
        X509Certificate2? certificate,
        DateTimeOffset validationTimeUtc)
    {
        if (validationTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The certificate-validation time must use UTC.",
                nameof(validationTimeUtc));
        }

        if (certificate is null)
        {
            return RuntimeHostClientCertificateValidationResult.Invalid(
                RuntimeHostClientCertificateValidationFailureReason
                    .CertificateMissing);
        }

        try
        {
            DateTimeOffset notBeforeUtc =
                new(
                    certificate.NotBefore.ToUniversalTime(),
                    TimeSpan.Zero);
            DateTimeOffset notAfterUtc =
                new(
                    certificate.NotAfter.ToUniversalTime(),
                    TimeSpan.Zero);

            if (validationTimeUtc < notBeforeUtc)
            {
                return RuntimeHostClientCertificateValidationResult.Invalid(
                    RuntimeHostClientCertificateValidationFailureReason
                        .CertificateNotYetValid);
            }

            if (validationTimeUtc > notAfterUtc)
            {
                return RuntimeHostClientCertificateValidationResult.Invalid(
                    RuntimeHostClientCertificateValidationFailureReason
                        .CertificateExpired);
            }

            X509Extension? enhancedKeyUsageExtension =
                certificate.Extensions
                    .Cast<X509Extension>()
                    .SingleOrDefault(
                        extension =>
                            string.Equals(
                                extension.Oid?.Value,
                                EnhancedKeyUsageExtensionOid,
                                StringComparison.Ordinal));

            if (enhancedKeyUsageExtension is not null)
            {
                X509EnhancedKeyUsageExtension enhancedKeyUsage =
                    enhancedKeyUsageExtension
                    as X509EnhancedKeyUsageExtension
                    ?? new X509EnhancedKeyUsageExtension(
                        enhancedKeyUsageExtension,
                        enhancedKeyUsageExtension.Critical);

                bool permitsClientAuthentication =
                    enhancedKeyUsage.EnhancedKeyUsages
                        .Cast<Oid>()
                        .Any(
                            usage =>
                                string.Equals(
                                    usage.Value,
                                    ClientAuthenticationUsageOid,
                                    StringComparison.Ordinal));

                if (!permitsClientAuthentication)
                {
                    return RuntimeHostClientCertificateValidationResult.Invalid(
                        RuntimeHostClientCertificateValidationFailureReason
                            .MissingClientAuthenticationUsage);
                }
            }

            return RuntimeHostClientCertificateValidationResult.Valid();
        }
        catch (CryptographicException)
        {
            return RuntimeHostClientCertificateValidationResult.Invalid(
                RuntimeHostClientCertificateValidationFailureReason
                    .MalformedCertificate);
        }
        catch (InvalidOperationException)
        {
            return RuntimeHostClientCertificateValidationResult.Invalid(
                RuntimeHostClientCertificateValidationFailureReason
                    .MalformedCertificate);
        }
    }
}
