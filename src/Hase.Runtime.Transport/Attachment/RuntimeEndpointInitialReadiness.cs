using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Waits for a supervised runtime endpoint to become initially ready.
/// </summary>
internal static class RuntimeEndpointInitialReadiness
{
    /// <summary>
    /// Waits until the runtime endpoint reaches the ready state while
    /// monitoring the task that owns connection supervision.
    /// </summary>
    internal static async Task WaitAsync(
        RuntimeEndpoint runtimeEndpoint,
        Task supervisionTask,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeEndpoint);

        ArgumentNullException.ThrowIfNull(
            supervisionTask);

        cancellationToken.ThrowIfCancellationRequested();

        var observer =
            new InitialReadinessObserver(
                runtimeEndpoint);

        runtimeEndpoint.SubscribeConnectionStatus(
            observer);

        using var waitCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        try
        {
            if (runtimeEndpoint.ConnectionStatus.State
                == EndpointConnectionState.Ready)
            {
                return;
            }

            Task readinessTask =
                observer.ReadinessTask.WaitAsync(
                    waitCancellationTokenSource.Token);

            Task completedTask =
                await Task.WhenAny(
                    readinessTask,
                    supervisionTask);

            if (observer.ReadinessTask.IsCompletedSuccessfully)
            {
                return;
            }

            if (ReferenceEquals(
                completedTask,
                readinessTask))
            {
                await readinessTask;

                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            await supervisionTask;

            throw new InvalidOperationException(
                "Endpoint connection supervision completed before "
                + "the runtime endpoint reached the ready state.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();

            throw;
        }
        finally
        {
            waitCancellationTokenSource.Cancel();

            runtimeEndpoint.UnsubscribeConnectionStatus(
                observer);
        }
    }

    private sealed class InitialReadinessObserver
        : IEndpointConnectionStatusObserver
    {
        private readonly RuntimeEndpoint _runtimeEndpoint;

        private readonly TaskCompletionSource _readiness =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        internal InitialReadinessObserver(
            RuntimeEndpoint runtimeEndpoint)
        {
            _runtimeEndpoint =
                runtimeEndpoint;
        }

        internal Task ReadinessTask =>
            _readiness.Task;

        public void OnEndpointConnectionStatusChanged(
            EndpointConnectionStatusChanged change)
        {
            ArgumentNullException.ThrowIfNull(
                change);

            if (ReferenceEquals(
                    change.Endpoint,
                    _runtimeEndpoint)
                && change.CurrentStatus.State
                    == EndpointConnectionState.Ready)
            {
                _readiness.TrySetResult();
            }
        }
    }
}