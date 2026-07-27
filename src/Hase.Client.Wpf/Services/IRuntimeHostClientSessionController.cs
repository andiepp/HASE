namespace Hase.Client.Wpf.Services;

public interface IRuntimeHostClientSessionController
    : IAsyncDisposable
{
    Task ConnectAsync(
        string configurationFilePath,
        CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}
