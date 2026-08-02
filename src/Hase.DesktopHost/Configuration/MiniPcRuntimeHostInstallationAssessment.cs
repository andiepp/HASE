namespace Hase.DesktopHost.Configuration;

/// <summary>
/// Summarizes fail-closed readiness of one new Arduino-only MiniPC Runtime
/// Host installation.
/// </summary>
public sealed record MiniPcRuntimeHostInstallationAssessment(
    bool SecurityPreflightReady,
    bool AuthoritativeArduinoReady,
    bool GuidedInstallationReady,
    bool IdentityCreated,
    bool InstallationAuditReady,
    bool ProvisionedSecurityPreserved)
{
    public IReadOnlyList<(string Name, bool Ready)> Readiness { get; } =
    [
        ("Security preflight", SecurityPreflightReady),
        ("Authoritative Arduino", AuthoritativeArduinoReady),
        ("Guided installation", GuidedInstallationReady),
        ("Runtime Host identity", IdentityCreated),
        ("Installation audit", InstallationAuditReady),
        ("Provisioned security", ProvisionedSecurityPreserved)
    ];

    public bool IsReady => Readiness.All(result => result.Ready);
}
