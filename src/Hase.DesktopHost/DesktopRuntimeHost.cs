namespace Hase.DesktopHost;

public sealed class DesktopRuntimeHost : IDesktopRuntimeHost, IAsyncDisposable
{
    private readonly IDesktopRuntimeHostBackend backend;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private DesktopRuntimeHostStatus status = DesktopRuntimeHostStatus.Stopped;
    private Exception? lastError;
    private bool disposed;

    public DesktopRuntimeHost(IDesktopRuntimeHostBackend backend)
    {
        this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public DesktopRuntimeHostStatus Status => status;

    public Exception? LastError => lastError;

    public event EventHandler<DesktopRuntimeHostStatusChangedEventArgs>? StatusChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (status is DesktopRuntimeHostStatus.Running or DesktopRuntimeHostStatus.Starting)
            {
                return;
            }

            if (status == DesktopRuntimeHostStatus.Stopping)
            {
                throw new InvalidOperationException(
                    "The desktop runtime host cannot start while it is stopping.");
            }

            lastError = null;
            SetStatus(DesktopRuntimeHostStatus.Starting);

            try
            {
                await backend.StartAsync(cancellationToken).ConfigureAwait(false);
                SetStatus(DesktopRuntimeHostStatus.Running);
            }
            catch (Exception exception)
            {
                lastError = exception;
                SetStatus(DesktopRuntimeHostStatus.Faulted);
                throw;
            }
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (status == DesktopRuntimeHostStatus.Stopped)
            {
                return;
            }

            if (status == DesktopRuntimeHostStatus.Starting)
            {
                throw new InvalidOperationException(
                    "The desktop runtime host cannot stop while it is starting.");
            }

            SetStatus(DesktopRuntimeHostStatus.Stopping);

            try
            {
                await backend.StopAsync(cancellationToken).ConfigureAwait(false);
                lastError = null;
                SetStatus(DesktopRuntimeHostStatus.Stopped);
            }
            catch (Exception exception)
            {
                lastError = exception;
                SetStatus(DesktopRuntimeHostStatus.Faulted);
                throw;
            }
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        if (status != DesktopRuntimeHostStatus.Stopped)
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }

        disposed = true;
        lifecycleGate.Dispose();
    }

    private void SetStatus(DesktopRuntimeHostStatus newStatus)
    {
        if (status == newStatus)
        {
            return;
        }

        var previousStatus = status;
        status = newStatus;

        StatusChanged?.Invoke(
            this,
            new DesktopRuntimeHostStatusChangedEventArgs(previousStatus, newStatus));
    }
}
