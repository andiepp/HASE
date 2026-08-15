namespace HASE.ProtocolExplorer.Deployment;

/// <summary>
/// Summarizes the fail-closed evidence required for an ADR-0054 deployment
/// bundle that has not yet been uploaded to a physical ESP32.
/// </summary>
public sealed record Esp32DeploymentBundleAssessment(
    bool ExactComputer,
    bool ExactRepositoryBaseline,
    bool ProcessesStopped,
    bool ToolchainReady,
    bool LocalSecretsReady,
    bool CurrentFirmwareCompiled,
    bool RollbackFirmwareCompiled,
    bool ArtifactCustodyReady,
    bool EvidenceSanitized,
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
        ("Local-secret readiness", LocalSecretsReady),
        ("Current firmware compilation", CurrentFirmwareCompiled),
        ("Rollback firmware compilation", RollbackFirmwareCompiled),
        ("Artifact custody", ArtifactCustodyReady),
        ("Sanitized evidence", EvidenceSanitized),
        ("Repository preservation", RepositoryUnchanged),
        ("Upload exclusion", UploadNotInvoked),
        ("Physical-state preservation", PhysicalStateUnchanged)
    ];

    public bool IsReady => Readiness.All(result => result.Ready);

    public bool RequiresRecovery => false;
}
