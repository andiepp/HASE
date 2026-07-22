using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactEndpointDefinitionTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly DescriptorPath EventPath =
        DescriptorPath.Parse(
            "Controller.ButtonPressed");

    [Fact]
    public void Constructor_ValidValues_ShouldExposeSnapshot()
    {
        // Arrange
        DescriptorReference descriptorReference =
            CreateDescriptorReference();

        EndpointDescriptorDefinition descriptorDefinition =
            CreateDescriptorDefinition();

        var propertyMappings =
            new List<CompactPropertyMapping>();

        var eventMappings =
            new List<CompactEventMapping>
            {
                CreateEventMapping()
            };

        // Act
        var definition =
            new CompactEndpointDefinition(
                descriptorReference,
                descriptorDefinition,
                propertyMappings,
                eventMappings);

        propertyMappings.Add(
            CreatePropertyMapping());

        eventMappings.Clear();

        // Assert
        Assert.Same(
            descriptorReference,
            definition.DescriptorReference);

        Assert.Same(
            descriptorDefinition,
            definition.DescriptorDefinition);

        Assert.Empty(
            definition.PropertyMappings);

        CompactEventMapping eventMapping =
            Assert.Single(
                definition.EventMappings);

        Assert.Equal(
            0x01,
            eventMapping.CompactEventId);
    }

    [Fact]
    public void ThreeArgumentConstructor_ShouldUseEmptyEventMappings()
    {
        var definition =
            new CompactEndpointDefinition(
                CreateDescriptorReference(),
                new EndpointDescriptorDefinition(),
                []);

        Assert.Empty(
            definition.EventMappings);
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
    public void Constructor_NullEventMappings_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEndpointDefinition(
                CreateDescriptorReference(),
                new EndpointDescriptorDefinition(),
                [],
                null!);
        }

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
    public void Constructor_NullEventMappingEntry_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEndpointDefinition(
                CreateDescriptorReference(),
                new EndpointDescriptorDefinition(),
                [],
                [
                    null!
                ]);
        }

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
                    CreatePropertyMapping()
                ]);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_UnknownEventTarget_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEndpointDefinition(
                CreateDescriptorReference(),
                new EndpointDescriptorDefinition(),
                [],
                [
                    CreateEventMapping()
                ]);
        }

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

    private static CompactPropertyMapping CreatePropertyMapping()
    {
        return new CompactPropertyMapping(
            compactPropertyId: 0x01,
            InstrumentId,
            new PropertyId(
                "led-state"),
            CompactPropertyValueEncoding.Boolean);
    }

    private static CompactEventMapping CreateEventMapping()
    {
        return new CompactEventMapping(
            compactEventId: 0x01,
            InstrumentId,
            EventPath,
            CompactEventValueEncoding.None);
    }

    private static EndpointDescriptorDefinition
        CreateDescriptorDefinition()
    {
        var eventDescriptor =
            new EventDescriptor(
                EventPath,
                "Button Pressed");

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Arduino Uno GPIO Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        events:
                        [
                            eventDescriptor
                        ])
            };

        return new EndpointDescriptorDefinition(
            instruments:
            [
                instrument
            ],
            metadata:
                new());
    }
}