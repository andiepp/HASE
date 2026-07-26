namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Represents the TLS acceptance decision for one presented client
/// certificate together with the authenticated HASE principal or the
/// deterministic C-030 failure details.
/// </summary>
public sealed record RuntimeHostMutualTlsClientCertificateAuthenticationResult
{
    private RuntimeHostMutualTlsClientCertificateAuthenticationResult(
        RuntimeHostClientPrincipal? principal,
        RuntimeHostCertificateAuthenticationFailureReason failureReason,
        RuntimeHostClientCertificateValidationFailureReason
            certificateValidationFailureReason,
        RuntimeHostCertificateTrustFailureReason trustFailureReason)
    {
        bool accepted = principal is not null;
        bool failed =
            failureReason
            != RuntimeHostCertificateAuthenticationFailureReason.None;

        if (accepted == failed)
        {
            throw new ArgumentException(
                "A mutual-TLS certificate result must contain either one "
                + "principal or one failure reason.");
        }

        Principal = principal;
        FailureReason = failureReason;
        CertificateValidationFailureReason =
            certificateValidationFailureReason;
        TrustFailureReason = trustFailureReason;
    }

    /// <summary>
    /// Gets the boolean decision returned to the TLS certificate callback.
    /// </summary>
    public bool IsAccepted =>
        Principal is not null;

    /// <summary>
    /// Gets the authenticated HASE client principal when accepted.
    /// </summary>
    public RuntimeHostClientPrincipal? Principal { get; }

    /// <summary>
    /// Gets the high-level C-030 failure stage when rejected.
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
    /// Creates one accepted TLS certificate result.
    /// </summary>
    public static RuntimeHostMutualTlsClientCertificateAuthenticationResult
        Accepted(
            RuntimeHostClientPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(
            principal);

        return new RuntimeHostMutualTlsClientCertificateAuthenticationResult(
            principal,
            RuntimeHostCertificateAuthenticationFailureReason.None,
            RuntimeHostClientCertificateValidationFailureReason.None,
            RuntimeHostCertificateTrustFailureReason.None);
    }

    /// <summary>
    /// Creates one rejected TLS certificate result while preserving the
    /// deterministic C-030 failure details.
    /// </summary>
    public static RuntimeHostMutualTlsClientCertificateAuthenticationResult
        Rejected(
            RuntimeHostCertificateAuthenticationFailureReason failureReason,
            RuntimeHostClientCertificateValidationFailureReason
                certificateValidationFailureReason,
            RuntimeHostCertificateTrustFailureReason trustFailureReason)
    {
        if (failureReason
            == RuntimeHostCertificateAuthenticationFailureReason.None)
        {
            throw new ArgumentException(
                "A rejection failure reason must be specified.",
                nameof(failureReason));
        }

        return new RuntimeHostMutualTlsClientCertificateAuthenticationResult(
            null,
            failureReason,
            certificateValidationFailureReason,
            trustFailureReason);
    }
}
