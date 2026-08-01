namespace Hase.Client.Configuration;

public interface IRuntimeHostProfileSessionController : IAsyncDisposable
{
    event EventHandler? SnapshotChanged;
    event EventHandler<RuntimeHostProfileEventOccurredEventArgs>? EventOccurred;
    RuntimeHostProfileSessionSnapshot Snapshot { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<RemotePropertyOperationResult> ReadPropertyAsync(RemotePropertyTarget target, CancellationToken cancellationToken = default);
    Task<RemotePropertyOperationResult> WritePropertyAsync(RemotePropertyTarget target, RemoteValue requestedValue, CancellationToken cancellationToken = default);
    Task<RemoteCommandOperationResult> ExecuteCommandAsync(RemoteCommandExecutionRequest request, CancellationToken cancellationToken = default);
}
