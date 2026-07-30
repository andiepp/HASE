using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost;

/// <summary>
/// Owns one bounded process-local Desktop Runtime Host diagnostic session.
/// </summary>
public sealed class DesktopRuntimeDiagnosticSession
    : IDesktopRuntimeDiagnosticSource
{
    public const int DefaultCapacity =
        2000;

    private readonly BoundedRuntimeDiagnosticCollector collector;

    public DesktopRuntimeDiagnosticSession(
        RuntimeDiagnosticLevel maximumLevel =
            RuntimeDiagnosticLevel.Operational,
        int capacity = DefaultCapacity)
    {
        collector =
            new BoundedRuntimeDiagnosticCollector(
                capacity,
                maximumLevel);

        Publisher =
            new RuntimeDiagnosticPublisher(
                collector);
    }

    public RuntimeDiagnosticLevel MaximumLevel =>
        collector.MaximumLevel;

    public RuntimeDiagnosticPublisher Publisher
    {
        get;
    }

    public IReadOnlyList<RuntimeDiagnosticRecord> CaptureDiagnostics()
    {
        return collector.GetSnapshot();
    }

    public void ClearDiagnostics()
    {
        collector.Clear();
    }
}
