using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactEventMapTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly DescriptorPath ButtonPressedEventPath =
        DescriptorPath.Parse(
            "Controller.ButtonPressed");

    [Fact]
    public void Constructor_ValidMapping_ShouldRetainDefinitionAndMapping()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        CompactEventMapping mapping =
            CreateMapping();

        var map =
            new CompactEventMap(
                definition,
                [
                    mapping
                ]);

        Assert.Same(
            definition,
            map.DescriptorDefinition);

        CompactEventMapping result =
            Assert.Single(
                map.Mappings);

        Assert.Same(
            mapping,
            result);
    }

    [Fact]
    public void Find_KnownCompactEventId_ShouldReturnMapping()
    {
        CompactEventMapping mapping =
            CreateMapping();

        var map =
            new CompactEventMap(
                CreateDefinition(),
                [
                    mapping
                ]);

        CompactEventMapping? result =
            map.Find(
                0x01);

        Assert.Same(
            mapping,
            result);
    }

    [Fact]
    public void Find_UnknownCompactEventId_ShouldReturnNull()
    {
        var map =
            new CompactEventMap(
                CreateDefinition(),
                [
                    CreateMapping()
                ]);

        CompactEventMapping? result =
            map.Find(
                0x02);

        Assert.Null(
            result);
    }

    [Fact]
    public void Find_ZeroCompactEventId_ShouldThrow()
    {
        var map =
            new CompactEventMap(
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
    public void Mapping_ZeroCompactEventId_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEventMapping(
                compactEventId: 0,
                InstrumentId,
                ButtonPressedEventPath,
                CompactEventValueEncoding.None);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Mapping_UndefinedEncoding_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEventMapping(
                compactEventId: 0x01,
                InstrumentId,
                ButtonPressedEventPath,
                (CompactEventValueEncoding)0xFF);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Mapping_NullInstrumentId_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEventMapping(
                compactEventId: 0x01,
                null!,
                ButtonPressedEventPath,
                CompactEventValueEncoding.None);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Mapping_NullEventPath_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEventMapping(
                compactEventId: 0x01,
                InstrumentId,
                null!,
                CompactEventValueEncoding.None);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_DuplicateCompactEventId_ShouldThrow()
    {
        DescriptorPath secondEventPath =
            DescriptorPath.Parse(
                "Controller.SecondEvent");

        EndpointDescriptorDefinition definition =
            CreateDefinition(
                ButtonPressedEventPath,
                secondEventPath);

        void Act()
        {
            _ = new CompactEventMap(
                definition,
                [
                    CreateMapping(),
                    new CompactEventMapping(
                        compactEventId: 0x01,
                        InstrumentId,
                        secondEventPath,
                        CompactEventValueEncoding.None)
                ]);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_DuplicateEventTarget_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEventMap(
                CreateDefinition(),
                [
                    CreateMapping(),
                    new CompactEventMapping(
                        compactEventId: 0x02,
                        InstrumentId,
                        ButtonPressedEventPath,
                        CompactEventValueEncoding.None)
                ]);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_UnknownInstrument_ShouldThrow()
    {
        var mapping =
            new CompactEventMapping(
                compactEventId: 0x01,
                new InstrumentId(
                    "unknown-instrument"),
                ButtonPressedEventPath,
                CompactEventValueEncoding.None);

        void Act()
        {
            _ = new CompactEventMap(
                CreateDefinition(),
                [
                    mapping
                ]);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_UnknownEvent_ShouldThrow()
    {
        var mapping =
            new CompactEventMapping(
                compactEventId: 0x01,
                InstrumentId,
                DescriptorPath.Parse(
                    "Controller.UnknownEvent"),
                CompactEventValueEncoding.None);

        void Act()
        {
            _ = new CompactEventMap(
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
            _ = new CompactEventMap(
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
            _ = new CompactEventMap(
                CreateDefinition(),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullMappingEntry_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEventMap(
                CreateDefinition(),
                [
                    null!
                ]);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    private static CompactEventMapping CreateMapping()
    {
        return new CompactEventMapping(
            compactEventId: 0x01,
            InstrumentId,
            ButtonPressedEventPath,
            CompactEventValueEncoding.None);
    }

    private static EndpointDescriptorDefinition CreateDefinition(
        params DescriptorPath[] eventPaths)
    {
        if (eventPaths.Length == 0)
        {
            eventPaths =
            [
                ButtonPressedEventPath
            ];
        }

        EventDescriptor[] events =
            eventPaths
                .Select(
                    eventPath =>
                        new EventDescriptor(
                            eventPath,
                            eventPath.ToString()))
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
                        events:
                            events)
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