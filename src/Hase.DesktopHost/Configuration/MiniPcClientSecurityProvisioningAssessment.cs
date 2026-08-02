namespace Hase.DesktopHost.Configuration;

/// <summary>
/// Summarizes fail-closed validation of laptop trust and client configuration
/// for a MiniPC Runtime Host.
/// </summary>
public sealed record MiniPcClientSecurityProvisioningAssessment(
    bool ClientConfigurationValid,
    bool ExistingClientPrivateKeyReady,
    bool TrustedServerCertificateReady,
    bool PublicServerCertificateMatches,
    bool ExistingClientStatePreserved)
{
    /// <summary>
    /// Gets the individually reportable validation results.
    /// </summary>
    public IReadOnlyList<(string Name, bool Ready)> Readiness { get; } =
    [
        ("MiniPC client configuration", ClientConfigurationValid),
        ("Existing client private key", ExistingClientPrivateKeyReady),
        ("Trusted server certificate", TrustedServerCertificateReady),
        ("Public certificate match", PublicServerCertificateMatches),
        ("Existing Client state", ExistingClientStatePreserved)
    ];

    /// <summary>
    /// Gets whether every laptop security-provisioning result is ready.
    /// </summary>
    public bool IsReady => Readiness.All(result => result.Ready);
}
