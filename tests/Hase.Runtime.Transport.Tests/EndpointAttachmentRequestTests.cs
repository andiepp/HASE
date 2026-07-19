using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class EndpointAttachmentRequestTests
{
    [Fact]
    public void Constructor_ValidValues_ShouldExposeValues()
    {
        // Arrange
        var connectionDefinition =
            new StubEndpointConnectionDefinition();

        IEndpointDescriptorSource descriptorSource =
            EndpointProvidedDescriptorSource.Instance;

        // Act
        var request =
            new EndpointAttachmentRequest(
                connectionDefinition,
                descriptorSource);

        // Assert
        Assert.Same(
            connectionDefinition,
            request.ConnectionDefinition);

        Assert.Same(
            descriptorSource,
            request.DescriptorSource);
    }

    [Fact]
    public void Constructor_NullConnectionDefinition_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new EndpointAttachmentRequest(
                null!,
                EndpointProvidedDescriptorSource.Instance);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullDescriptorSource_ShouldThrow()
    {
        // Arrange
        var connectionDefinition =
            new StubEndpointConnectionDefinition();

        // Act
        void Act()
        {
            _ = new EndpointAttachmentRequest(
                connectionDefinition,
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void EndpointProvidedDescriptorSource_ShouldImplementContract()
    {
        // Act
        EndpointProvidedDescriptorSource source =
            EndpointProvidedDescriptorSource.Instance;

        // Assert
        Assert.IsAssignableFrom<IEndpointDescriptorSource>(
            source);
    }

    [Fact]
    public void EndpointProvidedDescriptorSource_ShouldBeSingleton()
    {
        // Act
        EndpointProvidedDescriptorSource first =
            EndpointProvidedDescriptorSource.Instance;

        EndpointProvidedDescriptorSource second =
            EndpointProvidedDescriptorSource.Instance;

        // Assert
        Assert.Same(
            first,
            second);
    }

    private sealed class StubEndpointConnectionDefinition
        : IEndpointConnectionDefinition
    {
        public EndpointConnectionOrigin Origin =>
            EndpointConnectionOrigin.Configured;

        public EndpointId? ExpectedEndpointId =>
            null;
    }
}