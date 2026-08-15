namespace HASE.ProtocolExplorer.Deployment;

/// <summary>
/// Summarizes the fail-closed evidence required before a separately approved
/// ADR-0054 physical firmware upload may begin.
/// </summary>
public sealed record Esp32ControlledUploadReadinessAssessment(
    bool ExactComputer,
    bool ExactRepositoryBaseline,
    bool ProcessesStopped,
    bool ToolchainReady,
    bool BundleManifestReady,
    bool PreparationEvidenceReady,
    bool CurrentArtifactsReady,
    bool RollbackArtifactsReady,
    bool DeviceIdentityReady,
    bool PlanCustodyReady,
    bool RepositoryUnchanged,
    bool UploadNotInvoked,
    bool PhysicalStateUnchanged)
{
    public IReadOnlyList<(string Name, bool Ready)> Readiness { get; } =
    [
        ("Exact computer", ExactComputer),
        ("Exact repository baseline", ExactRepositoryBaseline),
        ("Processes stopped", ProcessesStopped),
        ("Toolchain readiness", ToolchainReady),
        ("Bundle-manifest readiness", BundleManifestReady),
        ("Preparation-evidence readiness", PreparationEvidenceReady),
        ("Current-artifact readiness", CurrentArtifactsReady),
        ("Rollback-artifact readiness", RollbackArtifactsReady),
        ("Device-identity readiness", DeviceIdentityReady),
        ("Plan custody", PlanCustodyReady),
        ("Repository preservation", RepositoryUnchanged),
        ("Upload exclusion", UploadNotInvoked),
        ("Physical-state preservation", PhysicalStateUnchanged)
    ];

    public bool IsReady => Readiness.All(result => result.Ready);

    public bool RequiresRecovery => false;
}
