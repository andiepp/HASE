namespace Hase.Client.Diagnostics;

/// <summary>
/// Disables client diagnostics without allocating records.
/// </summary>
public sealed class NullClientDiagnosticSink : IClientDiagnosticSink
{
    private NullClientDiagnosticSink()
    {
    }

    public static NullClientDiagnosticSink Instance { get; } = new();

    public bool IsEnabled(ClientDiagnosticLevel level) => false;

    public void Publish(ClientDiagnosticRecord record)
    {
    }
}
