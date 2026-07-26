using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Adapts the C-030 certificate-authentication pipeline to the boolean
/// acceptance decision required by a TLS client-certificate callback while
/// preserving the authenticated HASE principal for later HTTP-context
/// projection.
/// </summary>
public sealed class RuntimeHostMutualTlsClientCertificateAuthenticator
{
    private readonly IRuntimeHostCertificateAuthenticationService
        authenticationService;

    /// <summary>
    /// Initializes one mutual-TLS client-certificate authenticator.
    /// </summary>
    public RuntimeHostMutualTlsClientCertificateAuthenticator(
        IRuntimeHostCertificateAuthenticationService authenticationService)
    {
        this.authenticationService =
            authenticationService
            ?? throw new ArgumentNullException(
                nameof(authenticationService));
    }

    /// <summary>
    /// Authenticates one certificate at the explicit UTC authentication time.
    /// </summary>
    public RuntimeHostMutualTlsClientCertificateAuthenticationResult
        Authenticate(
            X509Certificate2? certificate,
            DateTimeOffset authenticatedAtUtc)
    {
        if (authenticatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The authentication timestamp must use UTC.",
                nameof(authenticatedAtUtc));
        }

        RuntimeHostCertificateAuthenticationResult result =
            authenticationService.Authenticate(
                certificate,
                authenticatedAtUtc)
            ?? throw new InvalidOperationException(
                "The certificate-authentication service returned null.");

        if (!result.IsAuthenticated)
        {
            return RuntimeHostMutualTlsClientCertificateAuthenticationResult
                .Rejected(
                    result.FailureReason,
                    result.CertificateValidationFailureReason,
                    result.TrustFailureReason);
        }

        return RuntimeHostMutualTlsClientCertificateAuthenticationResult
            .Accepted(
                result.Principal
                ?? throw new InvalidOperationException(
                    "Certificate authentication reported success without a "
                    + "principal."));
    }
}
