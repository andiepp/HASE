using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactEndpointInitializationResultTests
{
    [Fact]
    public void Constructor_ValidValues_ShouldExposeValues()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "uno-01");

        var descriptorReference =
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-environment"),
                version: 3);

        var descriptorDefinition =
            new EndpointDescriptorDefinition();

        EndpointDescriptor descriptor =
            descriptorDefinition.Materialize(
                endpointId);

        // Act
        var result =
            new CompactEndpointInitializationResult(
                endpointId,
                descriptorReference,
                descriptorDefinition,
                descriptor);

        // Assert
        Assert.Same(
            endpointId,
            result.EndpointId);

        Assert.Same(
            descriptorReference,
            result.DescriptorReference);

        Assert.Same(
            descriptorDefinition,
            result.DescriptorDefinition);

        Assert.Same(
            descriptor,
            result.Descriptor);
    }

    [Fact]
    public void Constructor_NullEndpointId_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactEndpointInitializationResult(
                null!,
                CreateDescriptorReference(),
                new EndpointDescriptorDefinition(),
                CreateDescriptor(
                    "uno-01"));
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullDescriptorReference_ShouldThrow()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "uno-01");

        // Act
        void Act()
        {
            _ = new CompactEndpointInitializationResult(
                endpointId,
                null!,
                new EndpointDescriptorDefinition(),
                CreateDescriptor(
                    endpointId.Value));
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullDescriptorDefinition_ShouldThrow()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "uno-01");

        // Act
        void Act()
        {
            _ = new CompactEndpointInitializationResult(
                endpointId,
                CreateDescriptorReference(),
                null!,
                CreateDescriptor(
                    endpointId.Value));
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
            _ = new CompactEndpointInitializationResult(
                new EndpointId(
                    "uno-01"),
                CreateDescriptorReference(),
                new EndpointDescriptorDefinition(),
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_MismatchedDescriptorIdentity_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactEndpointInitializationResult(
                new EndpointId(
                    "uno-01"),
                CreateDescriptorReference(),
                new EndpointDescriptorDefinition(),
                CreateDescriptor(
                    "uno-02"));
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    private static DescriptorReference CreateDescriptorReference()
    {
        return new DescriptorReference(
            new DescriptorId(
                "arduino-uno-environment"),
            version: 3);
    }

    private static EndpointDescriptor CreateDescriptor(
        string endpointId)
    {
        return new EndpointDescriptor(
            new EndpointId(
                endpointId));
    }
}