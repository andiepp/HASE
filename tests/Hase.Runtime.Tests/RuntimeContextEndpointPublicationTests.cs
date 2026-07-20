using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Tests;

public sealed class RuntimeContextEndpointPublicationTests
{
    [Fact]
    public void CreateEndpoint_ShouldNotPublishEndpoint()
    {
        // Arrange
        var context =
            new RuntimeContext();

        EndpointDescriptor descriptor =
            CreateDescriptor(
                "staged-endpoint");

        // Act
        RuntimeEndpoint endpoint =
            context.CreateEndpoint(
                descriptor);

        // Assert
        Assert.Same(
            context,
            endpoint.Context);

        Assert.Same(
            descriptor,
            endpoint.Descriptor);

        Assert.Empty(
            context.Endpoints);

        Assert.Null(
            context.FindEndpoint(
                descriptor.Id));
    }

    [Fact]
    public void PublishEndpoint_StagedEndpoint_ShouldPublishSameEndpoint()
    {
        // Arrange
        var context =
            new RuntimeContext();

        RuntimeEndpoint endpoint =
            context.CreateEndpoint(
                CreateDescriptor(
                    "staged-endpoint"));

        // Act
        RuntimeEndpoint publishedEndpoint =
            context.PublishEndpoint(
                endpoint);

        // Assert
        Assert.Same(
            endpoint,
            publishedEndpoint);

        Assert.Same(
            endpoint,
            Assert.Single(
                context.Endpoints));

        Assert.Same(
            endpoint,
            context.FindEndpoint(
                endpoint.Descriptor.Id));
    }

    [Fact]
    public void AddEndpoint_ShouldConstructAndPublishEndpoint()
    {
        // Arrange
        var context =
            new RuntimeContext();

        EndpointDescriptor descriptor =
            CreateDescriptor(
                "immediate-endpoint");

        // Act
        RuntimeEndpoint endpoint =
            context.AddEndpoint(
                descriptor);

        // Assert
        Assert.Same(
            context,
            endpoint.Context);

        Assert.Same(
            descriptor,
            endpoint.Descriptor);

        Assert.Same(
            endpoint,
            Assert.Single(
                context.Endpoints));
    }

    [Fact]
    public void PublishEndpoint_DifferentContext_ShouldThrow()
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
            _ = publishingContext.PublishEndpoint(
                endpoint);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);

        Assert.Empty(
            publishingContext.Endpoints);
    }

    [Fact]
    public void PublishEndpoint_DuplicateEndpointId_ShouldThrow()
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
            _ = context.PublishEndpoint(
                stagedEndpoint);
        }

        // Assert
        Assert.Throws<InvalidOperationException>(
            Act);

        Assert.Single(
            context.Endpoints);
    }

    [Fact]
    public void CreateEndpoint_NullDescriptor_ShouldThrow()
    {
        // Arrange
        var context =
            new RuntimeContext();

        // Act
        void Act()
        {
            _ = context.CreateEndpoint(
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void PublishEndpoint_NullEndpoint_ShouldThrow()
    {
        // Arrange
        var context =
            new RuntimeContext();

        // Act
        void Act()
        {
            _ = context.PublishEndpoint(
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static EndpointDescriptor CreateDescriptor(
        string endpointId)
    {
        return new EndpointDescriptor(
            new EndpointId(
                endpointId));
    }
}