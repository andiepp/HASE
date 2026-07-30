namespace Hase.Runtime.Diagnostics;

/// <summary>
/// Disables structured runtime diagnostics without allocating records.
/// </summary>
public sealed class NullRuntimeDiagnosticSink :
    IRuntimeDiagnosticSink
{
    private NullRuntimeDiagnosticSink()
    {
    }

    public static NullRuntimeDiagnosticSink Instance { get; } =
        new();

    public bool IsEnabled(
        RuntimeDiagnosticLevel level)
    {
        return false;
    }

    public void Publish(
        RuntimeDiagnosticRecord record)
    {
    }
}
