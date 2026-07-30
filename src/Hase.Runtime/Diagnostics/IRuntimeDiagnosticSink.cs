namespace Hase.Runtime.Diagnostics;

/// <summary>
/// Receives immutable runtime diagnostic records.
/// </summary>
public interface IRuntimeDiagnosticSink
{
    bool IsEnabled(
        RuntimeDiagnosticLevel level);

    void Publish(
        RuntimeDiagnosticRecord record);
}
