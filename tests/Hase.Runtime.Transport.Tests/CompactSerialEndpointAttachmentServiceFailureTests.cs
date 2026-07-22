using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactSerialEndpointAttachmentServiceFailureTests
{
    [Fact]
    public async Task AttachAsync_BootstrapFailure_ShouldNotResolveOrCreateResources()
    {
        // Arrange
        var expectedException =
            new IOException(
                "Compact bootstrap failed.");

        int resolverCallCount =
            0;

        int resourceFactoryCallCount =
            0;

        var runtimeContext =
            new RuntimeContext();

        var service =
            new CompactSerialEndpointAttachmentService(
                runtimeContext,
                (
                    connectionDefinition,
                    cancellationToken) =>
                    throw expectedException,
                (
                    bootstrapResult,
                    cancellationToken) =>
                {
                    resolverCallCount++;

                    throw new InvalidOperationException(
                        "Operational definition resolution was not expected.");
                },
                (
                    connectionDefinition,
                    definition,
                    runtimeEndpoint) =>
                {
                    resourceFactoryCallCount++;

                    throw new InvalidOperationException(
                        "Operational resource creation was not expected.");
                });

        // Act
        Task Act()
        {
            return service.AttachAsync(
                CreateRequest());
        }

        // Assert
        IOException actualException =
            await Assert.ThrowsAsync<IOException>(
                Act);

        Assert.Same(
            expectedException,
            actualException);

        Assert.Equal(
            0,
            resolverCallCount);

        Assert.Equal(
            0,
            resourceFactoryCallCount);

        Assert.Empty(
            runtimeContext.Endpoints);
    }

    [Fact]
    public async Task AttachAsync_DefinitionResolutionFailure_ShouldNotCreateResources()
    {
        // Arrange
        var expectedException =
            new InvalidDataException(
                "The operational compact definition changed.");

        int resourceFactoryCallCount =
            0;

        var runtimeContext =
            new RuntimeContext();

        CompactEndpointAttachmentBootstrapResult bootstrapResult =
            CreateBootstrapResult();

        var service =
            new CompactSerialEndpointAttachmentService(
                runtimeContext,
                (
                    connectionDefinition,
                    cancellationToken) =>
                    Task.FromResult(
                        bootstrapResult),
                (
                    receivedBootstrapResult,
                    cancellationToken) =>
                    throw expectedException,
                (
                    connectionDefinition,
                    definition,
                    runtimeEndpoint) =>
                {
                    resourceFactoryCallCount++;

                    throw new InvalidOperationException(
                        "Operational resource creation was not expected.");
                });

        // Act
        Task Act()
        {
            return service.AttachAsync(
                CreateRequest());
        }

        // Assert
        InvalidDataException actualException =
            await Assert.ThrowsAsync<InvalidDataException>(
                Act);

        Assert.Same(
            expectedException,
            actualException);

        Assert.Equal(
            0,
            resourceFactoryCallCount);

        Assert.Empty(
            runtimeContext.Endpoints);
    }

    [Fact]
    public async Task AttachAsync_SupervisionFailure_ShouldDisposeResourcesWithoutPublishing()
    {
        // Arrange
        var expectedException =
            new IOException(
                "Operational compact supervision failed.");

        var runtimeContext =
            new RuntimeContext();

        CompactEndpointAttachmentBootstrapResult bootstrapResult =
            CreateBootstrapResult();

        var definition =
            new CompactEndpointDefinition(
                bootstrapResult.DescriptorReference,
                bootstrapResult.DescriptorDefinition,
                []);

        var remainingResource =
            new TrackingAsyncDisposable();

        var service =
            new CompactSerialEndpointAttachmentService(
                runtimeContext,
                (
                    connectionDefinition,
                    cancellationToken) =>
                    Task.FromResult(
                        bootstrapResult),
                (
                    receivedBootstrapResult,
                    cancellationToken) =>
                    Task.FromResult(
                        definition),
                (
                    connectionDefinition,
                    receivedDefinition,
                    runtimeEndpoint) =>
                    new FailingOperationalResources(
                        expectedException,
                        remainingResource));

        // Act
        Task Act()
        {
            return service.AttachAsync(
                CreateRequest());
        }

        // Assert
        IOException actualException =
            await Assert.ThrowsAsync<IOException>(
                Act);

        Assert.Same(
            expectedException,
            actualException);

        Assert.Empty(
            runtimeContext.Endpoints);

        Assert.Equal(
            1,
            remainingResource.DisposeCallCount);
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

    private sealed class FailingOperationalResources
        : ICompactEndpointOperationalResources
    {
        public FailingOperationalResources(
            Exception exception,
            IAsyncDisposable remainingResource)
        {
            SupervisionLifetime =
                new EndpointConnectionSupervisionLifetime(
                    cancellationToken =>
                        Task.FromException(
                            exception));

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