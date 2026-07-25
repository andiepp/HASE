using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Composes local certificate validation, platform trust validation,
/// credential identity extraction, and enrollment-backed authentication.
/// </summary>
public sealed class RuntimeHostCertificateAuthenticationService
    : IRuntimeHostCertificateAuthenticationService
{
    private readonly IRuntimeHostClientCertificateValidator
        certificateValidator;
    private readonly IRuntimeHostCertificateTrustValidator
        trustValidator;
    private readonly IRuntimeHostX509ClientCredentialIdentityExtractor
        credentialIdentityExtractor;
    private readonly IRuntimeHostClientAuthenticationService
        authenticationService;

    /// <summary>
    /// Initializes the certificate-authentication pipeline.
    /// </summary>
    public RuntimeHostCertificateAuthenticationService(
        IRuntimeHostClientCertificateValidator certificateValidator,
        IRuntimeHostCertificateTrustValidator trustValidator,
        IRuntimeHostX509ClientCredentialIdentityExtractor
            credentialIdentityExtractor,
        IRuntimeHostClientAuthenticationService authenticationService)
    {
        this.certificateValidator =
            certificateValidator
            ?? throw new ArgumentNullException(
                nameof(certificateValidator));
        this.trustValidator =
            trustValidator
            ?? throw new ArgumentNullException(
                nameof(trustValidator));
        this.credentialIdentityExtractor =
            credentialIdentityExtractor
            ?? throw new ArgumentNullException(
                nameof(credentialIdentityExtractor));
        this.authenticationService =
            authenticationService
            ?? throw new ArgumentNullException(
                nameof(authenticationService));
    }

    /// <inheritdoc />
    public RuntimeHostCertificateAuthenticationResult Authenticate(
        X509Certificate2? certificate,
        DateTimeOffset authenticatedAtUtc)
    {
        if (authenticatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The authentication timestamp must use UTC.",
                nameof(authenticatedAtUtc));
        }

        RuntimeHostClientCertificateValidationResult validationResult =
            certificateValidator.Validate(
                certificate,
                authenticatedAtUtc)
            ?? throw new InvalidOperationException(
                "The client-certificate validator returned null.");

        if (!validationResult.IsValid)
        {
            return RuntimeHostCertificateAuthenticationResult
                .CertificateInvalid(
                    validationResult.FailureReason);
        }

        X509Certificate2 validatedCertificate =
            certificate
            ?? throw new InvalidOperationException(
                "The client-certificate validator accepted a missing "
                + "certificate.");

        RuntimeHostCertificateTrustValidationResult trustResult =
            trustValidator.Validate(
                validatedCertificate,
                authenticatedAtUtc)
            ?? throw new InvalidOperationException(
                "The certificate-trust validator returned null.");

        if (!trustResult.IsTrusted)
        {
            return RuntimeHostCertificateAuthenticationResult
                .CertificateUntrusted(
                    trustResult.FailureReason);
        }

        RuntimeHostClientCredentialIdentity credentialIdentity =
            credentialIdentityExtractor.Extract(
                validatedCertificate);

        RuntimeHostAuthenticationResult authenticationResult =
            authenticationService.Authenticate(
                credentialIdentity,
                authenticatedAtUtc)
            ?? throw new InvalidOperationException(
                "The client-authentication service returned null.");

        if (!authenticationResult.IsAuthenticated)
        {
            if (authenticationResult.FailureReason
                != RuntimeHostAuthenticationFailureReason.UnknownCredential)
            {
                throw new InvalidOperationException(
                    "The client-authentication service returned an "
                    + "unsupported failure reason.");
            }

            return RuntimeHostCertificateAuthenticationResult
                .UnknownCredential();
        }

        return RuntimeHostCertificateAuthenticationResult.Authenticated(
            authenticationResult.Principal
            ?? throw new InvalidOperationException(
                "The client-authentication service reported success without "
                + "returning a principal."));
    }
}
