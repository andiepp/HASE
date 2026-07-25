namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Represents the deterministic result of local client-certificate
/// validation.
/// </summary>
public sealed record RuntimeHostClientCertificateValidationResult
{
    private RuntimeHostClientCertificateValidationResult(
        RuntimeHostClientCertificateValidationFailureReason failureReason)
    {
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets a value indicating whether the certificate passed validation.
    /// </summary>
    public bool IsValid =>
        FailureReason
        == RuntimeHostClientCertificateValidationFailureReason.None;

    /// <summary>
    /// Gets the failure reason, or None when validation succeeded.
    /// </summary>
    public RuntimeHostClientCertificateValidationFailureReason FailureReason
    {
        get;
    }

    /// <summary>
    /// Creates one successful validation result.
    /// </summary>
    public static RuntimeHostClientCertificateValidationResult Valid()
    {
        return new RuntimeHostClientCertificateValidationResult(
            RuntimeHostClientCertificateValidationFailureReason.None);
    }

    /// <summary>
    /// Creates one failed validation result.
    /// </summary>
    public static RuntimeHostClientCertificateValidationResult Invalid(
        RuntimeHostClientCertificateValidationFailureReason failureReason)
    {
        if (failureReason
            == RuntimeHostClientCertificateValidationFailureReason.None)
        {
            throw new ArgumentException(
                "A certificate-validation failure reason must be specified.",
                nameof(failureReason));
        }

        if (!Enum.IsDefined(
            failureReason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureReason));
        }

        return new RuntimeHostClientCertificateValidationResult(
            failureReason);
    }
}
