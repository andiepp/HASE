namespace Hase.Client.Diagnostics;

/// <summary>
/// Receives immutable client diagnostic records.
/// </summary>
public interface IClientDiagnosticSink
{
    bool IsEnabled(ClientDiagnosticLevel level);
    void Publish(ClientDiagnosticRecord record);
}
