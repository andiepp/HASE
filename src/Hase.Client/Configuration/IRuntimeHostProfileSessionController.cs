namespace Hase.Client.Configuration;

public interface IRuntimeHostProfileSessionController : IAsyncDisposable
{
    event EventHandler? SnapshotChanged;
    RuntimeHostProfileSessionSnapshot Snapshot { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}
