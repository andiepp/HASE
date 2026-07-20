namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Owns cancellation and completion of one long-running endpoint
/// connection-supervision loop.
/// </summary>
public sealed class EndpointConnectionSupervisionLifetime
    : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task>
        _runSupervisionAsync;

    private readonly CancellationTokenSource
        _lifetimeCancellationTokenSource =
            new();

    private readonly object _syncRoot =
        new();

    private Task? _supervisionTask;

    private Task? _stopTask;

    private bool _disposed;

    /// <summary>
    /// Initializes a supervision lifetime.
    /// </summary>
    /// <param name="runSupervisionAsync">
    /// Starts the existing endpoint supervision loop with the
    /// lifetime-owned cancellation token.
    /// </param>
    public EndpointConnectionSupervisionLifetime(
        Func<CancellationToken, Task> runSupervisionAsync)
    {
        _runSupervisionAsync =
            runSupervisionAsync
            ?? throw new ArgumentNullException(
                nameof(runSupervisionAsync));
    }

    /// <summary>
    /// Starts supervision or returns the already existing supervision task.
    /// </summary>
    public Task RunAsync()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);

            if (_stopTask is not null)
            {
                throw new InvalidOperationException(
                    "Endpoint connection supervision cannot be started "
                    + "after shutdown has begun.");
            }

            _supervisionTask ??=
                RunCoreAsync();

            return _supervisionTask;
        }
    }

    /// <summary>
    /// Cancels supervision and waits for the supervision loop to finish.
    /// </summary>
    /// <remarks>
    /// Repeated calls share the same stop operation. Cancellation of the
    /// caller's wait does not restart or abandon the underlying shutdown.
    /// </remarks>
    public Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        Task stopTask;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            _stopTask ??=
                StopCoreAsync();

            stopTask =
                _stopTask;
        }

        return stopTask.WaitAsync(
            cancellationToken);
    }

    /// <summary>
    /// Stops supervision and releases the lifetime cancellation source.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }
        }

        await StopAsync();

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed =
                true;

            _lifetimeCancellationTokenSource.Dispose();
        }
    }

    private async Task RunCoreAsync()
    {
        await _runSupervisionAsync(
            _lifetimeCancellationTokenSource.Token);
    }

    private async Task StopCoreAsync()
    {
        Task? supervisionTask;

        lock (_syncRoot)
        {
            _lifetimeCancellationTokenSource.Cancel();

            supervisionTask =
                _supervisionTask;
        }

        if (supervisionTask is null)
        {
            return;
        }

        try
        {
            await supervisionTask;
        }
        catch (OperationCanceledException)
            when (_lifetimeCancellationTokenSource
                .IsCancellationRequested)
        {
        }
    }
}