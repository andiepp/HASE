using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class NativeEndpointBootstrapResultTests
{
    [Fact]
    public void Constructor_ValidValues_ShouldExposeValues()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "bootstrap-endpoint");

        var descriptor =
            new EndpointDescriptor(
                endpointId);

        // Act
        var result =
            new NativeEndpointBootstrapResult(
                endpointId,
                descriptor);

        // Assert
        Assert.Same(
            endpointId,
            result.EndpointId);

        Assert.Same(
            descriptor,
            result.Descriptor);
    }

    [Fact]
    public void Constructor_EquivalentEndpointIds_ShouldSucceed()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "bootstrap-endpoint");

        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "bootstrap-endpoint"));

        // Act
        var result =
            new NativeEndpointBootstrapResult(
                endpointId,
                descriptor);

        // Assert
        Assert.Equal(
            endpointId,
            result.Descriptor.Id);
    }

    [Fact]
    public void Constructor_NullEndpointId_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new NativeEndpointBootstrapResult(
                null!,
                new EndpointDescriptor(
                    new EndpointId(
                        "bootstrap-endpoint")));
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullDescriptor_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new NativeEndpointBootstrapResult(
                new EndpointId(
                    "bootstrap-endpoint"),
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_MismatchedEndpointId_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new NativeEndpointBootstrapResult(
                new EndpointId(
                    "bootstrap-endpoint"),
                new EndpointDescriptor(
                    new EndpointId(
                        "different-endpoint")));
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }
}