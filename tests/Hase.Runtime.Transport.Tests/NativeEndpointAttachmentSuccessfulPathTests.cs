using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class NativeEndpointAttachmentSuccessfulPathTests
{
    [Fact]
    public async Task CompleteAsync_ReadyEndpoint_ShouldPublishAndReturnSession()
    {
        var runtimeContext =
            new RuntimeContext();

        EndpointAttachmentRequest request =
            CreateRequest();

        NativeEndpointBootstrapResult bootstrapResult =
            CreateBootstrapResult();

        var supervisionStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var remainingResource =
            new RecordingAsyncDisposable();

        var successfulPath =
            new NativeEndpointAttachmentSuccessfulPath(
                runtimeContext);

        Task<EndpointAttachmentSession> completionTask =
            successfulPath.CompleteAsync(
                request,
                bootstrapResult,
                runtimeEndpoint =>
                    new EndpointConnectionSupervisionLifetime(
                        async cancellationToken =>
                        {
                            supervisionStarted.TrySetResult();

                            runtimeEndpoint.UpdateConnectionStatus(
                                new EndpointConnectionStatus(
                                    EndpointConnectionState.Ready,
                                    DateTimeOffset.UtcNow));

                            await Task.Delay(
                                Timeout.InfiniteTimeSpan,
                                cancellationToken);
                        }),
                [remainingResource]);

        await supervisionStarted.Task;

        EndpointAttachmentSession session =
            await completionTask;

        Assert.Same(
            request,
            session.Request);

        Assert.Same(
            bootstrapResult.Descriptor,
            session.RuntimeEndpoint.Descriptor);

        Assert.Equal(
            EndpointConnectionState.Ready,
            session.RuntimeEndpoint.ConnectionStatus.State);

        Assert.Same(
            session.RuntimeEndpoint,
            Assert.Single(
                runtimeContext.Endpoints));

        await session.ShutdownAsync();

        Assert.Empty(
            runtimeContext.Endpoints);

        Assert.Equal(
            1,
            remainingResource.DisposeCallCount);
    }

    [Fact]
    public async Task CompleteAsync_ShouldNotPublishBeforeReady()
    {
        var runtimeContext =
            new RuntimeContext();

        var allowReady =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var successfulPath =
            new NativeEndpointAttachmentSuccessfulPath(
                runtimeContext);

        NativeEndpointBootstrapResult bootstrapResult =
            CreateBootstrapResult();

        Task<EndpointAttachmentSession> completionTask =
            successfulPath.CompleteAsync(
                CreateRequest(),
                bootstrapResult,
                runtimeEndpoint =>
                    new EndpointConnectionSupervisionLifetime(
                        async cancellationToken =>
                        {
                            await allowReady.Task.WaitAsync(
                                cancellationToken);

                            runtimeEndpoint.UpdateConnectionStatus(
                                new EndpointConnectionStatus(
                                    EndpointConnectionState.Ready,
                                    DateTimeOffset.UtcNow));

                            await Task.Delay(
                                Timeout.InfiniteTimeSpan,
                                cancellationToken);
                        }),
                Array.Empty<IAsyncDisposable>());

        Assert.Empty(
            runtimeContext.Endpoints);

        allowReady.TrySetResult();

        EndpointAttachmentSession session =
            await completionTask;

        await session.DisposeAsync();
    }

    [Fact]
    public void Constructor_NullRuntimeContext_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new NativeEndpointAttachmentSuccessfulPath(
                null!));
    }

    [Fact]
    public async Task CompleteAsync_NullResourceCollection_ShouldThrow()
    {
        var successfulPath =
            new NativeEndpointAttachmentSuccessfulPath(
                new RuntimeContext());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => successfulPath.CompleteAsync(
                CreateRequest(),
                CreateBootstrapResult(),
                static runtimeEndpoint =>
                    new EndpointConnectionSupervisionLifetime(
                        static cancellationToken =>
                            Task.Delay(
                                Timeout.InfiniteTimeSpan,
                                cancellationToken)),
                null!));
    }

    [Fact]
    public async Task CompleteAsync_SupervisionFault_ShouldCleanUpAndPropagate()
    {
        var runtimeContext =
            new RuntimeContext();

        var remainingResource =
            new RecordingAsyncDisposable();

        var expectedException =
            new IOException(
                "Initial supervision failed.");

        var successfulPath =
            new NativeEndpointAttachmentSuccessfulPath(
                runtimeContext);

        IOException exception =
            await Assert.ThrowsAsync<IOException>(
                () => successfulPath.CompleteAsync(
                    CreateRequest(),
                    CreateBootstrapResult(),
                    runtimeEndpoint =>
                        new EndpointConnectionSupervisionLifetime(
                            cancellationToken =>
                                Task.FromException(
                                    expectedException)),
                    [remainingResource]));

        Assert.Same(
            expectedException,
            exception);

        Assert.Empty(
            runtimeContext.Endpoints);

        Assert.Equal(
            1,
            remainingResource.DisposeCallCount);
    }

    [Fact]
    public async Task CompleteAsync_CallerCancellation_ShouldCleanUpAndPropagate()
    {
        var runtimeContext =
            new RuntimeContext();

        var remainingResource =
            new RecordingAsyncDisposable();

        var supervisionStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var successfulPath =
            new NativeEndpointAttachmentSuccessfulPath(
                runtimeContext);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task<EndpointAttachmentSession> completionTask =
            successfulPath.CompleteAsync(
                CreateRequest(),
                CreateBootstrapResult(),
                runtimeEndpoint =>
                    new EndpointConnectionSupervisionLifetime(
                        async cancellationToken =>
                        {
                            supervisionStarted.TrySetResult();

                            await Task.Delay(
                                Timeout.InfiniteTimeSpan,
                                cancellationToken);
                        }),
                [remainingResource],
                cancellationTokenSource.Token);

        await supervisionStarted.Task;

        cancellationTokenSource.Cancel();

        OperationCanceledException exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await completionTask);

        Assert.Equal(
            cancellationTokenSource.Token,
            exception.CancellationToken);

        Assert.Empty(
            runtimeContext.Endpoints);

        Assert.Equal(
            1,
            remainingResource.DisposeCallCount);
    }

    [Fact]
    public async Task CompleteAsync_PublicationFailure_ShouldCleanUpAndPropagate()
    {
        var runtimeContext =
            new RuntimeContext();

        _ = runtimeContext.AddEndpoint(
            CreateBootstrapResult().Descriptor);

        var remainingResource =
            new RecordingAsyncDisposable();

        var successfulPath =
            new NativeEndpointAttachmentSuccessfulPath(
                runtimeContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => successfulPath.CompleteAsync(
                CreateRequest(),
                CreateBootstrapResult(),
                runtimeEndpoint =>
                    new EndpointConnectionSupervisionLifetime(
                        async cancellationToken =>
                        {
                            runtimeEndpoint.UpdateConnectionStatus(
                                new EndpointConnectionStatus(
                                    EndpointConnectionState.Ready,
                                    DateTimeOffset.UtcNow));

                            await Task.Delay(
                                Timeout.InfiniteTimeSpan,
                                cancellationToken);
                        }),
                [remainingResource]));

        Assert.Single(
            runtimeContext.Endpoints);

        Assert.Equal(
            1,
            remainingResource.DisposeCallCount);
    }

    private static EndpointAttachmentRequest CreateRequest()
    {
        return new EndpointAttachmentRequest(
            new StubEndpointConnectionDefinition(),
            EndpointProvidedDescriptorSource.Instance);
    }

    private static NativeEndpointBootstrapResult CreateBootstrapResult()
    {
        var endpointId =
            new EndpointId(
                "successful-path-endpoint");

        return new NativeEndpointBootstrapResult(
            endpointId,
            new EndpointDescriptor(
                endpointId));
    }

    private sealed class StubEndpointConnectionDefinition
        : IEndpointConnectionDefinition
    {
        public EndpointConnectionOrigin Origin =>
            EndpointConnectionOrigin.Configured;

        public EndpointId? ExpectedEndpointId =>
            null;
    }

    private sealed class RecordingAsyncDisposable
        : IAsyncDisposable
    {
        public int DisposeCallCount
        {
            get;
            private set;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            return ValueTask.CompletedTask;
        }
    }
}