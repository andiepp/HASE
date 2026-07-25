namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Identifies the stage at which certificate-backed client authentication
/// failed.
/// </summary>
public enum RuntimeHostCertificateAuthenticationFailureReason
{
    /// <summary>
    /// No authentication failure has occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// The certificate failed deterministic local validation.
    /// </summary>
    CertificateInvalid = 1,

    /// <summary>
    /// The certificate chain was not trusted.
    /// </summary>
    CertificateUntrusted = 2,

    /// <summary>
    /// The validated and trusted credential was not enrolled.
    /// </summary>
    UnknownCredential = 3
}
