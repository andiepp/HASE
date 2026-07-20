using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointPublicationTests
{
    [Fact]
    public void Publish_StagedEndpoint_ShouldPublishAndExposeValues()
    {
        // Arrange
        var context =
            new RuntimeContext();

        RuntimeEndpoint endpoint =
            context.CreateEndpoint(
                CreateDescriptor(
                    "published-endpoint"));

        // Act
        RuntimeEndpointPublication publication =
            RuntimeEndpointPublication.Publish(
                context,
                endpoint);

        // Assert
        Assert.Same(
            context,
            publication.Context);

        Assert.Same(
            endpoint,
            publication.Endpoint);

        Assert.Same(
            endpoint,
            Assert.Single(
                context.Endpoints));
    }

    [Fact]
    public void Publish_NullContext_ShouldThrow()
    {
        // Arrange
        RuntimeEndpoint endpoint =
            new RuntimeContext()
                .CreateEndpoint(
                    CreateDescriptor(
                        "published-endpoint"));

        // Act
        void Act()
        {
            _ = RuntimeEndpointPublication.Publish(
                null!,
                endpoint);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Publish_NullEndpoint_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = RuntimeEndpointPublication.Publish(
                new RuntimeContext(),
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Publish_DifferentContext_ShouldThrow()
    {
        // Arrange
        var owningContext =
            new RuntimeContext();

        var publishingContext =
            new RuntimeContext();

        RuntimeEndpoint endpoint =
            owningContext.CreateEndpoint(
                CreateDescriptor(
                    "foreign-endpoint"));

        // Act
        void Act()
        {
            _ = RuntimeEndpointPublication.Publish(
                publishingContext,
                endpoint);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);

        Assert.Empty(
            publishingContext.Endpoints);
    }

    [Fact]
    public void Publish_DuplicateEndpointId_ShouldThrow()
    {
        // Arrange
        var context =
            new RuntimeContext();

        _ = context.AddEndpoint(
            CreateDescriptor(
                "duplicate-endpoint"));

        RuntimeEndpoint stagedEndpoint =
            context.CreateEndpoint(
                CreateDescriptor(
                    "duplicate-endpoint"));

        // Act
        void Act()
        {
            _ = RuntimeEndpointPublication.Publish(
                context,
                stagedEndpoint);
        }

        // Assert
        Assert.Throws<InvalidOperationException>(
            Act);

        Assert.Single(
            context.Endpoints);
    }

    [Fact]
    public async Task DisposeAsync_RepeatedCall_ShouldRemoveEndpointOnce()
    {
        // Arrange
        var context =
            new RuntimeContext();

        RuntimeEndpoint endpoint =
            context.CreateEndpoint(
                CreateDescriptor(
                    "published-endpoint"));

        RuntimeEndpointPublication publication =
            RuntimeEndpointPublication.Publish(
                context,
                endpoint);

        // Act
        await publication.DisposeAsync();
        await publication.DisposeAsync();

        // Assert
        Assert.Empty(
            context.Endpoints);

        Assert.Null(
            context.FindEndpoint(
                endpoint.Descriptor.Id));
    }

    private static EndpointDescriptor CreateDescriptor(
        string endpointId)
    {
        return new EndpointDescriptor(
            new EndpointId(
                endpointId));
    }
}