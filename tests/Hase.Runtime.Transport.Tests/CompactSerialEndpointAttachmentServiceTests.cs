using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;
using Hase.Transport.Tcp;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactSerialEndpointAttachmentServiceTests
{
    [Fact]
    public void Constructor_NullRuntimeContext_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactSerialEndpointAttachmentService(
                null!,
                static (
                    connectionDefinition,
                    cancellationToken) =>
                    throw new InvalidOperationException(),
                static (
                    bootstrapResult,
                    cancellationToken) =>
                    throw new InvalidOperationException(),
                static (
                    connectionDefinition,
                    definition,
                    runtimeEndpoint) =>
                    throw new InvalidOperationException());
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task AttachAsync_UnsupportedDescriptorSource_ShouldRejectBeforeBootstrap()
    {
        // Arrange
        int bootstrapCallCount =
            0;

        CompactSerialEndpointAttachmentService service =
            CreateService(
                bootstrapAsync:
                    (
                        connectionDefinition,
                        cancellationToken) =>
                    {
                        bootstrapCallCount++;

                        return Task.FromResult(
                            CreateBootstrapResult());
                    });

        var request =
            new EndpointAttachmentRequest(
                CreateSerialConnectionDefinition(),
                EndpointProvidedDescriptorSource.Instance);

        // Act
        Task Act()
        {
            return service.AttachAsync(
                request);
        }

        // Assert
        await Assert.ThrowsAsync<NotSupportedException>(
            Act);

        Assert.Equal(
            0,
            bootstrapCallCount);
    }

    [Fact]
    public async Task AttachAsync_UnsupportedConnectionDefinition_ShouldRejectBeforeBootstrap()
    {
        // Arrange
        int bootstrapCallCount =
            0;

        CompactSerialEndpointAttachmentService service =
            CreateService(
                bootstrapAsync:
                    (
                        connectionDefinition,
                        cancellationToken) =>
                    {
                        bootstrapCallCount++;

                        return Task.FromResult(
                            CreateBootstrapResult());
                    });

        var request =
            new EndpointAttachmentRequest(
                NetworkEndpointConnectionDefinition.FromConfiguration(
                    new TcpTransportOptions(
                        "192.0.2.1",
                        5000)),
                HostRepositoryDescriptorSource.Instance);

        // Act
        Task Act()
        {
            return service.AttachAsync(
                request);
        }

        // Assert
        await Assert.ThrowsAsync<NotSupportedException>(
            Act);

        Assert.Equal(
            0,
            bootstrapCallCount);
    }

    [Fact]
    public async Task AttachAsync_ValidRequest_ShouldCompleteOwnedAttachment()
    {
        // Arrange
        var runtimeContext =
            new RuntimeContext();

        CompactEndpointAttachmentBootstrapResult bootstrapResult =
            CreateBootstrapResult();

        CompactEndpointDefinition operationalDefinition =
            new(
                bootstrapResult.DescriptorReference,
                bootstrapResult.DescriptorDefinition,
                []);

        SerialEndpointConnectionDefinition? receivedConnectionDefinition =
            null;

        CompactEndpointAttachmentBootstrapResult? receivedBootstrapResult =
            null;

        CompactEndpointDefinition? receivedOperationalDefinition =
            null;

        RuntimeEndpoint? receivedRuntimeEndpoint =
            null;

        var remainingResource =
            new TrackingAsyncDisposable();

        var service =
            new CompactSerialEndpointAttachmentService(
                runtimeContext,
                (
                    connectionDefinition,
                    cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    receivedConnectionDefinition =
                        connectionDefinition;

                    return Task.FromResult(
                        bootstrapResult);
                },
                (
                    receivedBootstrap,
                    cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    receivedBootstrapResult =
                        receivedBootstrap;

                    return Task.FromResult(
                        operationalDefinition);
                },
                (
                    connectionDefinition,
                    definition,
                    runtimeEndpoint) =>
                {
                    receivedOperationalDefinition =
                        definition;

                    receivedRuntimeEndpoint =
                        runtimeEndpoint;

                    return new ControlledOperationalResources(
                        runtimeEndpoint,
                        remainingResource);
                });

        EndpointAttachmentRequest request =
            CreateRequest();

        // Act
        IEndpointAttachmentSession session =
            await service.AttachAsync(
                request);

        // Assert
        Assert.Same(
            request.ConnectionDefinition,
            receivedConnectionDefinition);

        Assert.Same(
            bootstrapResult,
            receivedBootstrapResult);

        Assert.Same(
            operationalDefinition,
            receivedOperationalDefinition);

        Assert.Same(
            session.RuntimeEndpoint,
            receivedRuntimeEndpoint);

        Assert.Equal(
            EndpointConnectionState.Ready,
            session.RuntimeEndpoint.ConnectionStatus.State);

        Assert.Collection(
            runtimeContext.Endpoints,
            endpoint =>
                Assert.Same(
                    session.RuntimeEndpoint,
                    endpoint));

        await session.ShutdownAsync();

        Assert.Empty(
            runtimeContext.Endpoints);

        Assert.Equal(
            1,
            remainingResource.DisposeCallCount);
    }

    [Fact]
    public async Task AttachAsync_PreCancelledToken_ShouldNotBootstrap()
    {
        // Arrange
        int bootstrapCallCount =
            0;

        CompactSerialEndpointAttachmentService service =
            CreateService(
                bootstrapAsync:
                    (
                        connectionDefinition,
                        cancellationToken) =>
                    {
                        bootstrapCallCount++;

                        throw new InvalidOperationException(
                            "Bootstrap was not expected.");
                    });

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        Task Act()
        {
            return service.AttachAsync(
                CreateRequest(),
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            Act);

        Assert.Equal(
            0,
            bootstrapCallCount);
    }

    private static CompactSerialEndpointAttachmentService CreateService(
        Func<
            SerialEndpointConnectionDefinition,
            CancellationToken,
            Task<CompactEndpointAttachmentBootstrapResult>>?
            bootstrapAsync = null)
    {
        CompactEndpointAttachmentBootstrapResult bootstrapResult =
            CreateBootstrapResult();

        CompactEndpointDefinition operationalDefinition =
            new(
                bootstrapResult.DescriptorReference,
                bootstrapResult.DescriptorDefinition,
                []);

        return new CompactSerialEndpointAttachmentService(
            new RuntimeContext(),
            bootstrapAsync
                ?? ((
                    connectionDefinition,
                    cancellationToken) =>
                    Task.FromResult(
                        bootstrapResult)),
            (
                receivedBootstrapResult,
                cancellationToken) =>
                Task.FromResult(
                    operationalDefinition),
            (
                connectionDefinition,
                definition,
                runtimeEndpoint) =>
                new ControlledOperationalResources(
                    runtimeEndpoint,
                    new TrackingAsyncDisposable()));
    }

    private static EndpointAttachmentRequest CreateRequest()
    {
        return new EndpointAttachmentRequest(
            CreateSerialConnectionDefinition(),
            HostRepositoryDescriptorSource.Instance);
    }

    private static SerialEndpointConnectionDefinition
        CreateSerialConnectionDefinition()
    {
        return SerialEndpointConnectionDefinition.FromConfiguration(
            new SerialTransportOptions(
                "COM10",
                115200),
            new EndpointId(
                "arduino-uno-01"));
    }

    private static CompactEndpointAttachmentBootstrapResult
        CreateBootstrapResult()
    {
        var endpointId =
            new EndpointId(
                "arduino-uno-01");

        var descriptorReference =
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-validation"),
                version: 1);

        var descriptorDefinition =
            new EndpointDescriptorDefinition();

        return new CompactEndpointAttachmentBootstrapResult(
            endpointId,
            descriptorReference,
            descriptorDefinition,
            descriptorDefinition.Materialize(
                endpointId));
    }

    private sealed class ControlledOperationalResources
        : ICompactEndpointOperationalResources
    {
        private readonly RuntimeEndpoint _runtimeEndpoint;

        public ControlledOperationalResources(
            RuntimeEndpoint runtimeEndpoint,
            IAsyncDisposable remainingResource)
        {
            _runtimeEndpoint =
                runtimeEndpoint;

            SupervisionLifetime =
                new EndpointConnectionSupervisionLifetime(
                    RunAsync);

            ResourcesAfterSupervision =
            [
                remainingResource
            ];
        }

        public EndpointConnectionSupervisionLifetime SupervisionLifetime
        {
            get;
        }

        public IReadOnlyList<IAsyncDisposable> ResourcesAfterSupervision
        {
            get;
        }

        private async Task RunAsync(
            CancellationToken cancellationToken)
        {
            _runtimeEndpoint.UpdateConnectionStatus(
                new EndpointConnectionStatus(
                    EndpointConnectionState.Ready,
                    DateTimeOffset.UtcNow,
                    "Compact endpoint is ready."));

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }
    }

    private sealed class TrackingAsyncDisposable
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