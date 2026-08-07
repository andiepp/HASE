namespace Hase.Client;

/// <summary>Provides one single-use normalized Runtime Host diagnostic stream.</summary>
public interface IRemoteRuntimeDiagnosticStream : IAsyncDisposable
{
    IAsyncEnumerable<RemoteRuntimeDiagnosticObservation> ReadAsync(
        CancellationToken cancellationToken = default);
}
