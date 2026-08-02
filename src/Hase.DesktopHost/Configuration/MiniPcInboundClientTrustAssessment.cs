namespace Hase.DesktopHost.Configuration;

public sealed record MiniPcInboundClientTrustAssessment(
    bool ClientPrivateKeyPreserved,
    bool TransferCertificateIsPublicOnly,
    bool CredentialMatchesEnrollment,
    bool TrustedClientCertificateReady,
    bool RuntimeHostConfigurationPreserved,
    bool RuntimeHostIdentityPreserved)
{
    public bool IsReady =>
        ClientPrivateKeyPreserved
        && TransferCertificateIsPublicOnly
        && CredentialMatchesEnrollment
        && TrustedClientCertificateReady
        && RuntimeHostConfigurationPreserved
        && RuntimeHostIdentityPreserved;
}
