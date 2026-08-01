namespace Hase.DesktopHost.App.Hosting;

public sealed class DesktopRuntimeHostWindowShutdownCoordinator
{
    private readonly Func<CancellationToken, Task> stopAsync;
    private readonly object syncRoot = new();
    private Task? stopTask;

    public DesktopRuntimeHostWindowShutdownCoordinator(
        Func<CancellationToken, Task> stopAsync)
    {
        this.stopAsync = stopAsync
            ?? throw new ArgumentNullException(nameof(stopAsync));
    }

    public bool IsStarted
    {
        get
        {
            lock (syncRoot)
            {
                return stopTask is not null;
            }
        }
    }

    public bool IsCompleted
    {
        get
        {
            lock (syncRoot)
            {
                return stopTask?.IsCompleted == true;
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (syncRoot)
        {
            stopTask ??=
                stopAsync(cancellationToken);
            return stopTask;
        }
    }
}
