using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Validates that an externally provisioned certificate is suitable for one
/// private-network runtime-host listener.
/// </summary>
public static class RuntimeHostPrivateNetworkServerCertificateValidator
{
    private const string EnhancedKeyUsageExtensionOid =
        "2.5.29.37";

    private const string ServerAuthenticationUsageOid =
        "1.3.6.1.5.5.7.3.1";

    /// <summary>
    /// Validates the certificate at the supplied UTC time.
    /// </summary>
    public static void Validate(
        X509Certificate2 certificate,
        PrivateNetworkGrpcBinding binding,
        DateTimeOffset validationTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(
            certificate);
        ArgumentNullException.ThrowIfNull(
            binding);

        if (validationTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The server-certificate validation time must use UTC.",
                nameof(validationTimeUtc));
        }

        try
        {
            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException(
                    "The runtime-host server certificate does not have an "
                    + "accessible private key.");
            }

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
                throw new InvalidOperationException(
                    "The runtime-host server certificate is not yet valid.");
            }

            if (validationTimeUtc > notAfterUtc)
            {
                throw new InvalidOperationException(
                    "The runtime-host server certificate has expired.");
            }

            ValidateEnhancedKeyUsage(
                certificate);

            if (!certificate.MatchesHostname(
                    binding.Address.ToString(),
                    allowWildcards: false,
                    allowCommonName: false))
            {
                throw new InvalidOperationException(
                    "The runtime-host server certificate does not identify "
                    + "the configured listener address.");
            }
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException(
                "The runtime-host server certificate is malformed.",
                exception);
        }
    }

    private static void ValidateEnhancedKeyUsage(
        X509Certificate2 certificate)
    {
        X509Extension? enhancedKeyUsageExtension =
            certificate.Extensions
                .Cast<X509Extension>()
                .SingleOrDefault(
                    extension =>
                        string.Equals(
                            extension.Oid?.Value,
                            EnhancedKeyUsageExtensionOid,
                            StringComparison.Ordinal));

        if (enhancedKeyUsageExtension is null)
        {
            return;
        }

        X509EnhancedKeyUsageExtension enhancedKeyUsage =
            enhancedKeyUsageExtension
            as X509EnhancedKeyUsageExtension
            ?? new X509EnhancedKeyUsageExtension(
                enhancedKeyUsageExtension,
                enhancedKeyUsageExtension.Critical);

        bool permitsServerAuthentication =
            enhancedKeyUsage.EnhancedKeyUsages
                .Cast<Oid>()
                .Any(
                    usage =>
                        string.Equals(
                            usage.Value,
                            ServerAuthenticationUsageOid,
                            StringComparison.Ordinal));

        if (!permitsServerAuthentication)
        {
            throw new InvalidOperationException(
                "The runtime-host server certificate does not permit server "
                + "authentication.");
        }
    }
}
