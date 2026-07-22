using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactEndpointDefinitionTests
{
    [Fact]
    public void Constructor_ValidValues_ShouldExposeSnapshot()
    {
        // Arrange
        DescriptorReference descriptorReference =
            CreateDescriptorReference();

        var descriptorDefinition =
            new EndpointDescriptorDefinition();

        var propertyMappings =
            new List<CompactPropertyMapping>();

        // Act
        var definition =
            new CompactEndpointDefinition(
                descriptorReference,
                descriptorDefinition,
                propertyMappings);

        propertyMappings.Add(
            CreateMapping());

        // Assert
        Assert.Same(
            descriptorReference,
            definition.DescriptorReference);

        Assert.Same(
            descriptorDefinition,
            definition.DescriptorDefinition);

        Assert.Empty(
            definition.PropertyMappings);
    }

    [Fact]
    public void Constructor_NullDescriptorReference_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactEndpointDefinition(
                null!,
                new EndpointDescriptorDefinition(),
                []);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullDescriptorDefinition_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactEndpointDefinition(
                CreateDescriptorReference(),
                null!,
                []);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullPropertyMappings_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactEndpointDefinition(
                CreateDescriptorReference(),
                new EndpointDescriptorDefinition(),
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullPropertyMappingEntry_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactEndpointDefinition(
                CreateDescriptorReference(),
                new EndpointDescriptorDefinition(),
                [
                    null!
                ]);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_UnknownPropertyTarget_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactEndpointDefinition(
                CreateDescriptorReference(),
                new EndpointDescriptorDefinition(),
                [
                    CreateMapping()
                ]);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    private static DescriptorReference CreateDescriptorReference()
    {
        return new DescriptorReference(
            new DescriptorId(
                "arduino-uno-validation"),
            version: 1);
    }

    private static CompactPropertyMapping CreateMapping()
    {
        return new CompactPropertyMapping(
            compactPropertyId: 0x01,
            new InstrumentId(
                "controller-01"),
            new PropertyId(
                "led-state"),
            CompactPropertyValueEncoding.Boolean);
    }
}
