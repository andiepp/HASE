using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointInitialReadinessTests
{
    [Fact]
    public async Task WaitAsync_AlreadyReady_ShouldComplete()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        UpdateStatus(
            runtimeEndpoint,
            EndpointConnectionState.Ready);

        var supervisionCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        await RuntimeEndpointInitialReadiness.WaitAsync(
            runtimeEndpoint,
            supervisionCompletion.Task);
    }

    [Fact]
    public async Task WaitAsync_StatusBecomesReady_ShouldComplete()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var supervisionCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task waitTask =
            RuntimeEndpointInitialReadiness.WaitAsync(
                runtimeEndpoint,
                supervisionCompletion.Task);

        UpdateStatus(
            runtimeEndpoint,
            EndpointConnectionState.Ready);

        await waitTask;
    }

    [Fact]
    public async Task WaitAsync_CallerCancellation_ShouldPropagate()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var supervisionCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task waitTask =
            RuntimeEndpointInitialReadiness.WaitAsync(
                runtimeEndpoint,
                supervisionCompletion.Task,
                cancellationTokenSource.Token);

        cancellationTokenSource.Cancel();

        OperationCanceledException exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await waitTask);

        Assert.Equal(
            cancellationTokenSource.Token,
            exception.CancellationToken);
    }

    [Fact]
    public async Task WaitAsync_SupervisionFault_ShouldPropagate()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var expectedException =
            new IOException(
                "Connection supervision failed.");

        Task supervisionTask =
            Task.FromException(
                expectedException);

        IOException exception =
            await Assert.ThrowsAsync<IOException>(
                () => RuntimeEndpointInitialReadiness.WaitAsync(
                    runtimeEndpoint,
                    supervisionTask));

        Assert.Same(
            expectedException,
            exception);
    }

    [Fact]
    public async Task WaitAsync_NormalSupervisionCompletionBeforeReady_ShouldThrow()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => RuntimeEndpointInitialReadiness.WaitAsync(
                    runtimeEndpoint,
                    Task.CompletedTask));

        Assert.Equal(
            "Endpoint connection supervision completed before "
            + "the runtime endpoint reached the ready state.",
            exception.Message);
    }

    [Fact]
    public async Task WaitAsync_ReadyTransitionRacesWithInitialCheck_ShouldComplete()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var supervisionCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task waitTask =
            Task.Run(
                () => RuntimeEndpointInitialReadiness.WaitAsync(
                    runtimeEndpoint,
                    supervisionCompletion.Task));

        UpdateStatus(
            runtimeEndpoint,
            EndpointConnectionState.Ready);

        await waitTask.WaitAsync(
            TimeSpan.FromSeconds(
                1));
    }

    [Fact]
    public async Task WaitAsync_AfterCompletion_ShouldUnsubscribeObserver()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var supervisionCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task waitTask =
            RuntimeEndpointInitialReadiness.WaitAsync(
                runtimeEndpoint,
                supervisionCompletion.Task);

        UpdateStatus(
            runtimeEndpoint,
            EndpointConnectionState.Ready);

        await waitTask;

        UpdateStatus(
            runtimeEndpoint,
            EndpointConnectionState.Faulted);

        UpdateStatus(
            runtimeEndpoint,
            EndpointConnectionState.Ready);
    }

    [Fact]
    public async Task WaitAsync_NullRuntimeEndpoint_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => RuntimeEndpointInitialReadiness.WaitAsync(
                null!,
                Task.CompletedTask));
    }

    [Fact]
    public async Task WaitAsync_NullSupervisionTask_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => RuntimeEndpointInitialReadiness.WaitAsync(
                CreateRuntimeEndpoint(),
                null!));
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        return new RuntimeContext()
            .CreateEndpoint(
                new EndpointDescriptor(
                    new EndpointId(
                        "initial-readiness-endpoint")));
    }

    private static void UpdateStatus(
        RuntimeEndpoint runtimeEndpoint,
        EndpointConnectionState state)
    {
        runtimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                state,
                DateTimeOffset.UtcNow));
    }
}