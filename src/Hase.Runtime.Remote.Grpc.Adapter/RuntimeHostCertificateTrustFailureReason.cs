namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Identifies a deterministic certificate-chain trust failure.
/// </summary>
public enum RuntimeHostCertificateTrustFailureReason
{
    /// <summary>
    /// No trust failure has occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// No certificate was supplied for trust evaluation.
    /// </summary>
    CertificateMissing = 1,

    /// <summary>
    /// The platform could not build a chain to a trusted root.
    /// </summary>
    ChainNotTrusted = 2,

    /// <summary>
    /// The platform trust engine could not evaluate the certificate.
    /// </summary>
    TrustEvaluationFailed = 3
}
