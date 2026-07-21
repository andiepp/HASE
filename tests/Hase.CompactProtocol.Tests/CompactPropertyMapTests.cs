using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactPropertyMapTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly PropertyId LedStatePropertyId =
        new(
            "led-state");

    [Fact]
    public void Constructor_ValidMapping_ShouldRetainDefinitionAndMapping()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        CompactPropertyMapping mapping =
            CreateMapping();

        var map =
            new CompactPropertyMap(
                definition,
                [
                    mapping
                ]);

        Assert.Same(
            definition,
            map.DescriptorDefinition);

        CompactPropertyMapping result =
            Assert.Single(
                map.Mappings);

        Assert.Same(
            mapping,
            result);
    }

    [Fact]
    public void Find_KnownCompactPropertyId_ShouldReturnMapping()
    {
        CompactPropertyMapping mapping =
            CreateMapping();

        var map =
            new CompactPropertyMap(
                CreateDefinition(),
                [
                    mapping
                ]);

        CompactPropertyMapping? result =
            map.Find(
                0x01);

        Assert.Same(
            mapping,
            result);
    }

    [Fact]
    public void Find_UnknownCompactPropertyId_ShouldReturnNull()
    {
        var map =
            new CompactPropertyMap(
                CreateDefinition(),
                [
                    CreateMapping()
                ]);

        CompactPropertyMapping? result =
            map.Find(
                0x02);

        Assert.Null(
            result);
    }

    [Fact]
    public void Find_ZeroCompactPropertyId_ShouldThrow()
    {
        var map =
            new CompactPropertyMap(
                CreateDefinition(),
                [
                    CreateMapping()
                ]);

        void Act()
        {
            _ = map.Find(
                0);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Mapping_ZeroCompactPropertyId_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactPropertyMapping(
                compactPropertyId: 0,
                InstrumentId,
                LedStatePropertyId,
                CompactPropertyValueEncoding.Boolean);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Mapping_UndefinedEncoding_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactPropertyMapping(
                compactPropertyId: 0x01,
                InstrumentId,
                LedStatePropertyId,
                (CompactPropertyValueEncoding)0xFF);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Constructor_DuplicateCompactPropertyId_ShouldThrow()
    {
        var secondPropertyId =
            new PropertyId(
                "second-property");

        EndpointDescriptorDefinition definition =
            CreateDefinition(
                LedStatePropertyId,
                secondPropertyId);

        void Act()
        {
            _ = new CompactPropertyMap(
                definition,
                [
                    CreateMapping(),
                    new CompactPropertyMapping(
                        compactPropertyId: 0x01,
                        InstrumentId,
                        secondPropertyId,
                        CompactPropertyValueEncoding.Boolean)
                ]);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_DuplicatePropertyTarget_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactPropertyMap(
                CreateDefinition(),
                [
                    CreateMapping(),
                    new CompactPropertyMapping(
                        compactPropertyId: 0x02,
                        InstrumentId,
                        LedStatePropertyId,
                        CompactPropertyValueEncoding.Boolean)
                ]);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_UnknownInstrument_ShouldThrow()
    {
        var mapping =
            new CompactPropertyMapping(
                compactPropertyId: 0x01,
                new InstrumentId(
                    "unknown-instrument"),
                LedStatePropertyId,
                CompactPropertyValueEncoding.Boolean);

        void Act()
        {
            _ = new CompactPropertyMap(
                CreateDefinition(),
                [
                    mapping
                ]);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_UnknownProperty_ShouldThrow()
    {
        var mapping =
            new CompactPropertyMapping(
                compactPropertyId: 0x01,
                InstrumentId,
                new PropertyId(
                    "unknown-property"),
                CompactPropertyValueEncoding.Boolean);

        void Act()
        {
            _ = new CompactPropertyMap(
                CreateDefinition(),
                [
                    mapping
                ]);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_NullDefinition_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactPropertyMap(
                null!,
                []);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullMappings_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactPropertyMap(
                CreateDefinition(),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static CompactPropertyMapping CreateMapping()
    {
        return new CompactPropertyMapping(
            compactPropertyId: 0x01,
            InstrumentId,
            LedStatePropertyId,
            CompactPropertyValueEncoding.Boolean);
    }

    private static EndpointDescriptorDefinition CreateDefinition(
        params PropertyId[] propertyIds)
    {
        if (propertyIds.Length == 0)
        {
            propertyIds =
            [
                LedStatePropertyId
            ];
        }

        PropertyDescriptor[] properties =
            propertyIds
                .Select(
                    propertyId =>
                        new PropertyDescriptor(
                            propertyId,
                            new DescriptorPath(
                                propertyId.Value),
                            propertyId.Value,
                            new TestDataDescriptor())
                        {
                            AccessMode =
                                PropertyAccessMode.Read
                        })
                .ToArray();

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Arduino Uno GPIO Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                            properties)
            };

        return new EndpointDescriptorDefinition(
            instruments:
            [
                instrument
            ],
            metadata:
                new());
    }

    private sealed record TestDataDescriptor
        : DataDescriptor;
}