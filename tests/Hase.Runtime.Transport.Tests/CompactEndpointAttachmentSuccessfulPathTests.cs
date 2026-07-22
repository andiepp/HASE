using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactEndpointAttachmentSuccessfulPathTests
{
    [Fact]
    public void Constructor_NullRuntimeContext_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactEndpointAttachmentSuccessfulPath(
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task CompleteAsync_ReadyEndpoint_ShouldPublishAndReturnOwningSession()
    {
        // Arrange
        var runtimeContext =
            new RuntimeContext();

        var successfulPath =
            new CompactEndpointAttachmentSuccessfulPath(
                runtimeContext);

        EndpointAttachmentRequest request =
            CreateRequest();

        CompactEndpointAttachmentBootstrapResult bootstrapResult =
            CreateBootstrapResult();

        var remainingResource =
            new TrackingAsyncDisposable();

        ControlledCompactOperationalResources? resources =
            null;

        // Act
        EndpointAttachmentSession session =
            await successfulPath.CompleteAsync(
                request,
                bootstrapResult,
                runtimeEndpoint =>
                {
                    resources =
                        new ControlledCompactOperationalResources(
                            runtimeEndpoint,
                            remainingResource);

                    return resources;
                });

        // Assert
        Assert.Same(
            request,
            session.Request);

        Assert.Equal(
            bootstrapResult.EndpointId,
            session.RuntimeEndpoint.Descriptor.Id);

        Assert.Equal(
            EndpointConnectionState.Ready,
            session.RuntimeEndpoint.ConnectionStatus.State);

        Assert.Collection(
            runtimeContext.Endpoints,
            endpoint =>
                Assert.Same(
                    session.RuntimeEndpoint,
                    endpoint));

        Assert.NotNull(
            resources);

        Assert.Equal(
            1,
            resources.RunCallCount);

        Assert.Equal(
            0,
            remainingResource.DisposeCallCount);

        await session.ShutdownAsync();

        Assert.Empty(
            runtimeContext.Endpoints);

        Assert.Equal(
            1,
            remainingResource.DisposeCallCount);
    }

    [Fact]
    public async Task CompleteAsync_NullBootstrapResult_ShouldThrow()
    {
        // Arrange
        var successfulPath =
            new CompactEndpointAttachmentSuccessfulPath(
                new RuntimeContext());

        // Act
        Task Act()
        {
            return successfulPath.CompleteAsync(
                CreateRequest(),
                null!,
                runtimeEndpoint =>
                    throw new InvalidOperationException(
                        "The resource factory was not expected."));
        }

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task CompleteAsync_NullResourceFactory_ShouldThrow()
    {
        // Arrange
        var successfulPath =
            new CompactEndpointAttachmentSuccessfulPath(
                new RuntimeContext());

        // Act
        Task Act()
        {
            return successfulPath.CompleteAsync(
                CreateRequest(),
                CreateBootstrapResult(),
                null!);
        }

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);
    }

    private static EndpointAttachmentRequest CreateRequest()
    {
        return new EndpointAttachmentRequest(
            SerialEndpointConnectionDefinition.FromConfiguration(
                new SerialTransportOptions(
                    "COM10",
                    115200),
                new EndpointId(
                    "arduino-uno-01")),
            HostRepositoryDescriptorSource.Instance);
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

    private sealed class ControlledCompactOperationalResources
        : ICompactEndpointOperationalResources
    {
        private readonly RuntimeEndpoint _runtimeEndpoint;

        public ControlledCompactOperationalResources(
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

        public int RunCallCount
        {
            get;
            private set;
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
            RunCallCount++;

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