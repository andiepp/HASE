namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Represents the deterministic result of certificate-chain trust validation.
/// </summary>
public sealed record RuntimeHostCertificateTrustValidationResult
{
    private RuntimeHostCertificateTrustValidationResult(
        RuntimeHostCertificateTrustFailureReason failureReason)
    {
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets a value indicating whether the certificate chain is trusted.
    /// </summary>
    public bool IsTrusted =>
        FailureReason
        == RuntimeHostCertificateTrustFailureReason.None;

    /// <summary>
    /// Gets the failure reason, or None when trust validation succeeded.
    /// </summary>
    public RuntimeHostCertificateTrustFailureReason FailureReason { get; }

    /// <summary>
    /// Creates one successful trust-validation result.
    /// </summary>
    public static RuntimeHostCertificateTrustValidationResult Trusted()
    {
        return new RuntimeHostCertificateTrustValidationResult(
            RuntimeHostCertificateTrustFailureReason.None);
    }

    /// <summary>
    /// Creates one failed trust-validation result.
    /// </summary>
    public static RuntimeHostCertificateTrustValidationResult Untrusted(
        RuntimeHostCertificateTrustFailureReason failureReason)
    {
        if (failureReason
            == RuntimeHostCertificateTrustFailureReason.None)
        {
            throw new ArgumentException(
                "A certificate-trust failure reason must be specified.",
                nameof(failureReason));
        }

        if (!Enum.IsDefined(
            failureReason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureReason));
        }

        return new RuntimeHostCertificateTrustValidationResult(
            failureReason);
    }
}
