namespace Hase.Client.Deployment;

/// <summary>
/// Summarizes the fail-closed result of onboarding one MiniPC Runtime Host
/// profile without starting either Runtime Host or the Client.
/// </summary>
public sealed record MiniPcClientProfileOnboardingAssessment(
    bool HandoffValidated,
    bool ExistingDesktopProfilePreserved,
    bool MiniPcProfileEnabled,
    bool DistinctAuthoritativeIdentities,
    bool PrivateConfigurationsPreserved,
    bool RegistryBackupRetained)
{
    public IReadOnlyList<(string Name, bool Ready)> Readiness { get; } =
    [
        ("Handoff validation", HandoffValidated),
        ("Desktop profile preservation", ExistingDesktopProfilePreserved),
        ("MiniPC profile enablement", MiniPcProfileEnabled),
        ("Distinct authoritative identities", DistinctAuthoritativeIdentities),
        ("Private configuration preservation", PrivateConfigurationsPreserved),
        ("Registry backup retention", RegistryBackupRetained)
    ];

    public bool IsReady => Readiness.All(result => result.Ready);
}
