using System.Security.Cryptography.X509Certificates;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Owns the isolated C-032 certificates and their C-030 authentication
/// composition.
/// </summary>
internal sealed class CapabilityC032AuthenticationComposition
    : IDisposable
{
    private const string PrincipalId =
        "client-01";

    private const string TrustPolicyId =
        "c032-physical-validation-v1";

    private bool disposed;

    private CapabilityC032AuthenticationComposition(
        CapabilityC032CertificateSet certificates,
        IRuntimeHostCertificateAuthenticationService authenticationService)
    {
        Certificates =
            certificates;

        AuthenticationService =
            authenticationService;
    }

    /// <summary>
    /// Gets the isolated certificates owned by this composition.
    /// </summary>
    public CapabilityC032CertificateSet Certificates
    {
        get;
    }

    /// <summary>
    /// Gets the complete C-030 certificate-authentication pipeline.
    /// </summary>
    public IRuntimeHostCertificateAuthenticationService AuthenticationService
    {
        get;
    }

    /// <summary>
    /// Creates the physical-validation authentication composition.
    /// </summary>
    public static CapabilityC032AuthenticationComposition Create(
        DateTimeOffset validationTimeUtc)
    {
        if (validationTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The validation time must use UTC.",
                nameof(validationTimeUtc));
        }

        CapabilityC032CertificateSet? certificates =
            null;

        try
        {
            certificates =
                CapabilityC032CertificateSet.Create(
                    validationTimeUtc);

            var identityExtractor =
                new RuntimeHostX509ClientCredentialIdentityExtractor();
            RuntimeHostClientCredentialIdentity credentialIdentity =
                identityExtractor.Extract(
                    certificates.ClientCertificate);
            var enrollmentRegistry =
                new RuntimeHostClientCredentialEnrollmentRegistry(
                    new[]
                    {
                        new RuntimeHostClientCredentialEnrollment(
                            credentialIdentity,
                            new RuntimeHostClientPrincipalId(
                                PrincipalId),
                            TrustPolicyId)
                    });
            var authenticationService =
                new RuntimeHostCertificateAuthenticationService(
                    new RuntimeHostClientCertificateValidator(),
                    new RuntimeHostCertificateTrustValidator(
                        new ExactCertificateTrustEvaluator(
                            certificates.ClientCertificate)),
                    identityExtractor,
                    new RuntimeHostClientAuthenticationService(
                        enrollmentRegistry));

            var result =
                new CapabilityC032AuthenticationComposition(
                    certificates,
                    authenticationService);

            certificates =
                null;

            return result;
        }
        finally
        {
            certificates?.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed =
            true;

        Certificates.Dispose();
    }

    private sealed class ExactCertificateTrustEvaluator
        : IRuntimeHostCertificateTrustEvaluator
    {
        private readonly string trustedThumbprint;

        public ExactCertificateTrustEvaluator(
            X509Certificate2 trustedCertificate)
        {
            trustedThumbprint =
                trustedCertificate.Thumbprint;
        }

        public bool IsTrusted(
            X509Certificate2 certificate,
            DateTimeOffset validationTimeUtc)
        {
            return string.Equals(
                trustedThumbprint,
                certificate.Thumbprint,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
