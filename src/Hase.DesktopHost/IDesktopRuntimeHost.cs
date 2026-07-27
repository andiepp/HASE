namespace Hase.DesktopHost;

public interface IDesktopRuntimeHost
{
    DesktopRuntimeHostStatus Status { get; }

    Exception? LastError { get; }

    event EventHandler<DesktopRuntimeHostStatusChangedEventArgs>? StatusChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
