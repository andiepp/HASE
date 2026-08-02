namespace Hase.DesktopHost.Configuration;

/// <summary>
/// Summarizes the fail-closed validation performed after provisioning the
/// security material for a distinct second Runtime Host.
/// </summary>
public sealed record SecondPcRuntimeHostSecurityProvisioningAssessment(
    bool PrivateNetworkConfigurationValid,
    bool ClientEnrollmentValid,
    bool ServerCertificateHasPrivateKey,
    bool PublicCertificateMatches,
    bool ListenerAddressOwned)
{
    /// <summary>
    /// Gets the individually reportable validation results.
    /// </summary>
    public IReadOnlyList<(string Name, bool Ready)> Readiness { get; } =
    [
        ("Private-network configuration", PrivateNetworkConfigurationValid),
        ("Client enrollment", ClientEnrollmentValid),
        ("Server certificate private key", ServerCertificateHasPrivateKey),
        ("Public server certificate", PublicCertificateMatches),
        ("Listener address ownership", ListenerAddressOwned)
    ];

    /// <summary>
    /// Gets whether every security-provisioning result is ready.
    /// </summary>
    public bool IsReady => Readiness.All(result => result.Ready);
}
