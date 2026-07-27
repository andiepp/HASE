namespace Hase.Client.Wpf.Services;

public interface IRuntimeHostClientSessionController
    : IAsyncDisposable
{
    Task ConnectAsync(
        string configurationFilePath,
        CancellationToken cancellationToken = default);

    Task DisconnectAsync();

    Task<RemotePropertyOperationResult> ReadPropertyAsync(
        RemotePropertyTarget target,
        CancellationToken cancellationToken = default);

    Task<RemotePropertyOperationResult> WritePropertyAsync(
        RemotePropertyTarget target,
        RemoteValue requestedValue,
        CancellationToken cancellationToken = default);

    Task<RemoteCommandOperationResult> ExecuteCommandAsync(
        RemoteCommandExecutionRequest request,
        CancellationToken cancellationToken = default);
}
