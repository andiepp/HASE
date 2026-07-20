using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Tcp;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class NativeNetworkEndpointAttachmentServiceTests
{
    [Fact]
    public async Task AttachAsync_SupportedRequest_ShouldBootstrapAndAttach()
    {
        var runtimeContext =
            new RuntimeContext();

        NetworkEndpointConnectionDefinition connectionDefinition =
            CreateConnectionDefinition();

        var request =
            new EndpointAttachmentRequest(
                connectionDefinition,
                EndpointProvidedDescriptorSource.Instance);

        NativeEndpointBootstrapResult bootstrapResult =
            CreateBootstrapResult();

        NetworkEndpointConnectionDefinition? bootstrapDefinition =
            null;

        NetworkEndpointConnectionDefinition? operationalDefinition =
            null;

        RuntimeEndpoint? operationalEndpoint =
            null;

        var remainingResource =
            new RecordingAsyncDisposable();

        var service =
            new NativeNetworkEndpointAttachmentService(
                runtimeContext,
                (
                    definition,
                    cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bootstrapDefinition =
                        definition;

                    return Task.FromResult(
                        bootstrapResult);
                },
                (
                    definition,
                    runtimeEndpoint) =>
                {
                    operationalDefinition =
                        definition;

                    operationalEndpoint =
                        runtimeEndpoint;

                    return new TestOperationalResources(
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
                        [remainingResource]);
                });

        IEndpointAttachmentSession session =
            await service.AttachAsync(
                request);

        Assert.Same(
            connectionDefinition,
            bootstrapDefinition);

        Assert.Same(
            connectionDefinition,
            operationalDefinition);

        Assert.Same(
            operationalEndpoint,
            session.RuntimeEndpoint);

        Assert.Same(
            bootstrapResult.Descriptor,
            session.RuntimeEndpoint.Descriptor);

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
    public async Task AttachAsync_UnsupportedDescriptorSource_ShouldReject()
    {
        bool bootstrapCalled =
            false;

        var service =
            CreateService(
                (
                    definition,
                    cancellationToken) =>
                {
                    bootstrapCalled =
                        true;

                    return Task.FromResult(
                        CreateBootstrapResult());
                });

        var request =
            new EndpointAttachmentRequest(
                CreateConnectionDefinition(),
                new TestDescriptorSource());

        await Assert.ThrowsAsync<NotSupportedException>(
            () => service.AttachAsync(
                request));

        Assert.False(
            bootstrapCalled);
    }

    [Fact]
    public async Task AttachAsync_UnsupportedConnectionDefinition_ShouldReject()
    {
        bool bootstrapCalled =
            false;

        var service =
            CreateService(
                (
                    definition,
                    cancellationToken) =>
                {
                    bootstrapCalled =
                        true;

                    return Task.FromResult(
                        CreateBootstrapResult());
                });

        var request =
            new EndpointAttachmentRequest(
                new TestConnectionDefinition(),
                EndpointProvidedDescriptorSource.Instance);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => service.AttachAsync(
                request));

        Assert.False(
            bootstrapCalled);
    }

    [Fact]
    public async Task AttachAsync_CallerCancellationDuringBootstrap_ShouldPropagate()
    {
        var runtimeContext =
            new RuntimeContext();

        var bootstrapStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var service =
            new NativeNetworkEndpointAttachmentService(
                runtimeContext,
                async (
                    definition,
                    cancellationToken) =>
                {
                    bootstrapStarted.TrySetResult();

                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "The bootstrap cancellation wait completed.");
                },
                (
                    definition,
                    runtimeEndpoint) =>
                    throw new InvalidOperationException(
                        "Operational resources must not be created."));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task<IEndpointAttachmentSession> attachmentTask =
            service.AttachAsync(
                new EndpointAttachmentRequest(
                    CreateConnectionDefinition(),
                    EndpointProvidedDescriptorSource.Instance),
                cancellationTokenSource.Token);

        await bootstrapStarted.Task;

        cancellationTokenSource.Cancel();

        OperationCanceledException exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await attachmentTask);

        Assert.Equal(
            cancellationTokenSource.Token,
            exception.CancellationToken);

        Assert.Empty(
            runtimeContext.Endpoints);
    }

    [Fact]
    public async Task AttachAsync_NullRequest_ShouldThrow()
    {
        NativeNetworkEndpointAttachmentService service =
            CreateService(
                (
                    definition,
                    cancellationToken) =>
                    Task.FromResult(
                        CreateBootstrapResult()));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.AttachAsync(
                null!));
    }

    [Fact]
    public async Task AttachAsync_OperationalConstructionFailure_ShouldNotPublish()
    {
        var runtimeContext =
            new RuntimeContext();

        var expectedException =
            new InvalidOperationException(
                "Operational construction failed.");

        var service =
            new NativeNetworkEndpointAttachmentService(
                runtimeContext,
                (
                    definition,
                    cancellationToken) =>
                    Task.FromResult(
                        CreateBootstrapResult()),
                (
                    definition,
                    runtimeEndpoint) =>
                    throw expectedException);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AttachAsync(
                    new EndpointAttachmentRequest(
                        CreateConnectionDefinition(),
                        EndpointProvidedDescriptorSource.Instance)));

        Assert.Same(
            expectedException,
            exception);

        Assert.Empty(
            runtimeContext.Endpoints);
    }

    [Fact]
    public async Task AttachAsync_SupervisionFailure_ShouldCleanUpAndNotPublish()
    {
        var runtimeContext =
            new RuntimeContext();

        var expectedException =
            new IOException(
                "Initial supervision failed.");

        var remainingResource =
            new RecordingAsyncDisposable();

        var service =
            new NativeNetworkEndpointAttachmentService(
                runtimeContext,
                (
                    definition,
                    cancellationToken) =>
                    Task.FromResult(
                        CreateBootstrapResult()),
                (
                    definition,
                    runtimeEndpoint) =>
                    new TestOperationalResources(
                        new EndpointConnectionSupervisionLifetime(
                            cancellationToken =>
                                Task.FromException(
                                    expectedException)),
                        [remainingResource]));

        IOException exception =
            await Assert.ThrowsAsync<IOException>(
                () => service.AttachAsync(
                    new EndpointAttachmentRequest(
                        CreateConnectionDefinition(),
                        EndpointProvidedDescriptorSource.Instance)));

        Assert.Same(
            expectedException,
            exception);

        Assert.Empty(
            runtimeContext.Endpoints);

        Assert.Equal(
            1,
            remainingResource.DisposeCallCount);
    }

    private static NativeNetworkEndpointAttachmentService CreateService(
        Func<
            NetworkEndpointConnectionDefinition,
            CancellationToken,
            Task<NativeEndpointBootstrapResult>>
            bootstrapAsync)
    {
        return new NativeNetworkEndpointAttachmentService(
            new RuntimeContext(),
            bootstrapAsync,
            (
                definition,
                runtimeEndpoint) =>
                throw new InvalidOperationException(
                    "Operational resources must not be created."));
    }

    private static NetworkEndpointConnectionDefinition
        CreateConnectionDefinition()
    {
        return NetworkEndpointConnectionDefinition.FromConfiguration(
            new TcpTransportOptions(
                "192.0.2.1",
                5000),
            new EndpointId(
                "attachment-service-endpoint"));
    }

    private static NativeEndpointBootstrapResult CreateBootstrapResult()
    {
        var endpointId =
            new EndpointId(
                "attachment-service-endpoint");

        return new NativeEndpointBootstrapResult(
            endpointId,
            new EndpointDescriptor(
                endpointId));
    }

    private sealed class TestDescriptorSource
        : IEndpointDescriptorSource
    {
    }

    private sealed class TestConnectionDefinition
        : IEndpointConnectionDefinition
    {
        public EndpointConnectionOrigin Origin =>
            EndpointConnectionOrigin.Configured;

        public EndpointId? ExpectedEndpointId =>
            null;
    }

    private sealed class TestOperationalResources
        : INativeEndpointOperationalResources
    {
        public TestOperationalResources(
            EndpointConnectionSupervisionLifetime supervisionLifetime,
            IReadOnlyList<IAsyncDisposable> resourcesAfterSupervision)
        {
            SupervisionLifetime =
                supervisionLifetime;

            ResourcesAfterSupervision =
                resourcesAfterSupervision;
        }

        public EndpointConnectionSupervisionLifetime SupervisionLifetime
        {
            get;
        }

        public IReadOnlyList<IAsyncDisposable> ResourcesAfterSupervision
        {
            get;
        }
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