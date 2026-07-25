namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Identifies a deterministic local client-certificate validation failure.
/// </summary>
public enum RuntimeHostClientCertificateValidationFailureReason
{
    /// <summary>
    /// No validation failure has occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// No client certificate was presented.
    /// </summary>
    CertificateMissing = 1,

    /// <summary>
    /// The certificate validity interval has not started.
    /// </summary>
    CertificateNotYetValid = 2,

    /// <summary>
    /// The certificate validity interval has ended.
    /// </summary>
    CertificateExpired = 3,

    /// <summary>
    /// An Enhanced Key Usage extension is present but does not permit client
    /// authentication.
    /// </summary>
    MissingClientAuthenticationUsage = 4,

    /// <summary>
    /// The certificate could not be interpreted by the local validation
    /// policy.
    /// </summary>
    MalformedCertificate = 5
}
