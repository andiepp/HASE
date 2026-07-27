namespace Hase.DesktopHost;

public interface IDesktopRuntimeHostBackend
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
