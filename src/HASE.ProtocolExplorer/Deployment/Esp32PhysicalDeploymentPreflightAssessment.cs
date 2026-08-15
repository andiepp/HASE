namespace HASE.ProtocolExplorer.Deployment;

/// <summary>
/// Summarizes the fail-closed readiness evidence required before any
/// ADR-0054 ESP32 firmware deployment is separately authorized.
/// </summary>
public sealed record Esp32PhysicalDeploymentPreflightAssessment(
    bool ExactComputer,
    bool ExactRepositoryBaseline,
    bool ProcessesStopped,
    bool ToolchainReady,
    bool LocalSecretsReady,
    bool ApplicationSourceReady,
    bool OperatorPortSelectionReady,
    bool RollbackSourceReady,
    bool RepositoryUnchanged)
{
    public IReadOnlyList<(string Name, bool Ready)> Readiness { get; } =
    [
        ("Exact computer", ExactComputer),
        ("Exact repository baseline", ExactRepositoryBaseline),
        ("Processes stopped", ProcessesStopped),
        ("Toolchain readiness", ToolchainReady),
        ("Local-secret readiness", LocalSecretsReady),
        ("Application-source readiness", ApplicationSourceReady),
        ("Operator port selection", OperatorPortSelectionReady),
        ("Rollback-source readiness", RollbackSourceReady),
        ("Repository preservation", RepositoryUnchanged)
    ];

    public bool IsReady => Readiness.All(result => result.Ready);

    public bool RequiresRecovery => false;
}
