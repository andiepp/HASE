namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Represents the complete result of certificate-backed HASE client
/// authentication.
/// </summary>
public sealed record RuntimeHostCertificateAuthenticationResult
{
    private RuntimeHostCertificateAuthenticationResult(
        RuntimeHostClientPrincipal? principal,
        RuntimeHostCertificateAuthenticationFailureReason failureReason,
        RuntimeHostClientCertificateValidationFailureReason
            certificateValidationFailureReason,
        RuntimeHostCertificateTrustFailureReason trustFailureReason)
    {
        bool authenticated = principal is not null;
        bool failed =
            failureReason
            != RuntimeHostCertificateAuthenticationFailureReason.None;

        if (authenticated == failed)
        {
            throw new ArgumentException(
                "A certificate-authentication result must contain either one "
                + "authenticated principal or one failure reason.");
        }

        Principal = principal;
        FailureReason = failureReason;
        CertificateValidationFailureReason =
            certificateValidationFailureReason;
        TrustFailureReason = trustFailureReason;
    }

    /// <summary>
    /// Gets a value indicating whether certificate authentication succeeded.
    /// </summary>
    public bool IsAuthenticated =>
        Principal is not null;

    /// <summary>
    /// Gets the authenticated principal, or null when authentication failed.
    /// </summary>
    public RuntimeHostClientPrincipal? Principal { get; }

    /// <summary>
    /// Gets the high-level certificate-authentication failure stage.
    /// </summary>
    public RuntimeHostCertificateAuthenticationFailureReason FailureReason
    {
        get;
    }

    /// <summary>
    /// Gets the local certificate-validation failure when applicable.
    /// </summary>
    public RuntimeHostClientCertificateValidationFailureReason
        CertificateValidationFailureReason { get; }

    /// <summary>
    /// Gets the certificate-trust failure when applicable.
    /// </summary>
    public RuntimeHostCertificateTrustFailureReason TrustFailureReason
    {
        get;
    }

    /// <summary>
    /// Creates one successful certificate-authentication result.
    /// </summary>
    public static RuntimeHostCertificateAuthenticationResult Authenticated(
        RuntimeHostClientPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(
            principal);

        return new RuntimeHostCertificateAuthenticationResult(
            principal,
            RuntimeHostCertificateAuthenticationFailureReason.None,
            RuntimeHostClientCertificateValidationFailureReason.None,
            RuntimeHostCertificateTrustFailureReason.None);
    }

    /// <summary>
    /// Creates one local certificate-validation failure.
    /// </summary>
    public static RuntimeHostCertificateAuthenticationResult
        CertificateInvalid(
            RuntimeHostClientCertificateValidationFailureReason failureReason)
    {
        if (failureReason
            == RuntimeHostClientCertificateValidationFailureReason.None)
        {
            throw new ArgumentException(
                "A certificate-validation failure reason must be specified.",
                nameof(failureReason));
        }

        return new RuntimeHostCertificateAuthenticationResult(
            null,
            RuntimeHostCertificateAuthenticationFailureReason
                .CertificateInvalid,
            failureReason,
            RuntimeHostCertificateTrustFailureReason.None);
    }

    /// <summary>
    /// Creates one certificate-trust failure.
    /// </summary>
    public static RuntimeHostCertificateAuthenticationResult
        CertificateUntrusted(
            RuntimeHostCertificateTrustFailureReason failureReason)
    {
        if (failureReason
            == RuntimeHostCertificateTrustFailureReason.None)
        {
            throw new ArgumentException(
                "A certificate-trust failure reason must be specified.",
                nameof(failureReason));
        }

        return new RuntimeHostCertificateAuthenticationResult(
            null,
            RuntimeHostCertificateAuthenticationFailureReason
                .CertificateUntrusted,
            RuntimeHostClientCertificateValidationFailureReason.None,
            failureReason);
    }

    /// <summary>
    /// Creates one unknown-credential authentication failure.
    /// </summary>
    public static RuntimeHostCertificateAuthenticationResult
        UnknownCredential()
    {
        return new RuntimeHostCertificateAuthenticationResult(
            null,
            RuntimeHostCertificateAuthenticationFailureReason
                .UnknownCredential,
            RuntimeHostClientCertificateValidationFailureReason.None,
            RuntimeHostCertificateTrustFailureReason.None);
    }
}
