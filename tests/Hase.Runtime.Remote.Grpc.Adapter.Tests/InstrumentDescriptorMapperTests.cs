using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class InstrumentDescriptorMapperTests
{
    [Fact]
    public void Constructor_NullChildMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "propertyDescriptorMapper",
            () =>
                new InstrumentDescriptorMapper(
                    null!,
                    new TestCommandMapper(),
                    new TestEventMapper()));

        Assert.Throws<ArgumentNullException>(
            "commandDescriptorMapper",
            () =>
                new InstrumentDescriptorMapper(
                    new TestPropertyMapper(),
                    null!,
                    new TestEventMapper()));

        Assert.Throws<ArgumentNullException>(
            "eventDescriptorMapper",
            () =>
                new InstrumentDescriptorMapper(
                    new TestPropertyMapper(),
                    new TestCommandMapper(),
                    null!));
    }

    [Fact]
    public void Map_NullDescriptor_ShouldThrow()
    {
        InstrumentDescriptorMapper mapper =
            CreateMapper();

        Assert.Throws<ArgumentNullException>(
            "descriptor",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_IdentityWithoutMetadata_ShouldLeaveOptionalsAbsent()
    {
        InstrumentDescriptorMapper mapper =
            CreateMapper();

        GrpcV1.InstrumentDescriptor result =
            mapper.Map(
                CreateInstrument());

        Assert.Equal(
            "instrument-1",
            result.InstrumentId);
        Assert.Equal(
            "Instrument 1",
            result.Name);
        Assert.Equal(
            "validation",
            result.Kind);
        Assert.False(result.HasManufacturer);
        Assert.False(result.HasModel);
        Assert.False(result.HasSerialNumber);
        Assert.False(result.HasFirmwareVersion);
        Assert.False(result.HasHardwareRevision);
        Assert.False(result.HasDescription);
        Assert.Empty(result.Properties);
        Assert.Empty(result.Commands);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void Map_Metadata_ShouldPreserveOptionalValues()
    {
        InstrumentDescriptorMapper mapper =
            CreateMapper();

        GrpcV1.InstrumentDescriptor result =
            mapper.Map(
                CreateInstrument() with
                {
                    Metadata =
                        new InstrumentMetadata
                        {
                            Manufacturer = "HASE",
                            Model = "Model 1",
                            SerialNumber = "SN-1",
                            FirmwareVersion = "1.2.3",
                            HardwareRevision = "A",
                            Description = "Validation instrument."
                        }
                });

        Assert.Equal("HASE", result.Manufacturer);
        Assert.Equal("Model 1", result.Model);
        Assert.Equal("SN-1", result.SerialNumber);
        Assert.Equal("1.2.3", result.FirmwareVersion);
        Assert.Equal("A", result.HardwareRevision);
        Assert.Equal("Validation instrument.", result.Description);
    }

    [Fact]
    public void Map_Interface_ShouldDelegateEachCollectionInOrder()
    {
        PropertyDescriptor property =
            CreateProperty();
        CommandDescriptor command =
            new(
                new DescriptorPath(
                    "Command"),
                "Command");
        EventDescriptor eventDescriptor =
            new(
                new DescriptorPath(
                    "Event"),
                "Event");

        var mappedProperty =
            new GrpcV1.PropertyDescriptor();
        var mappedCommand =
            new GrpcV1.CommandDescriptor();
        var mappedEvent =
            new GrpcV1.EventDescriptor();

        var propertyMapper =
            new TestPropertyMapper(
                mappedProperty);
        var commandMapper =
            new TestCommandMapper(
                mappedCommand);
        var eventMapper =
            new TestEventMapper(
                mappedEvent);

        var mapper =
            new InstrumentDescriptorMapper(
                propertyMapper,
                commandMapper,
                eventMapper);

        GrpcV1.InstrumentDescriptor result =
            mapper.Map(
                CreateInstrument() with
                {
                    Interface =
                        new InstrumentInterface(
                            new[] { property },
                            new[] { command },
                            new[] { eventDescriptor })
                });

        Assert.Same(property, Assert.Single(propertyMapper.Inputs));
        Assert.Same(command, Assert.Single(commandMapper.Inputs));
        Assert.Same(eventDescriptor, Assert.Single(eventMapper.Inputs));
        Assert.Same(mappedProperty, Assert.Single(result.Properties));
        Assert.Same(mappedCommand, Assert.Single(result.Commands));
        Assert.Same(mappedEvent, Assert.Single(result.Events));
    }

    [Fact]
    public void Map_PropertyMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new InstrumentDescriptorMapper(
                new TestPropertyMapper(
                    new GrpcV1.PropertyDescriptor[] { null! }),
                new TestCommandMapper(),
                new TestEventMapper());

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreateInstrument() with
                        {
                            Interface =
                                new InstrumentInterface(
                                    properties:
                                        new[] { CreateProperty() })
                        }));

        Assert.Equal(
            "The Property descriptor mapper returned null.",
            exception.Message);
    }

    [Fact]
    public void Map_CommandMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new InstrumentDescriptorMapper(
                new TestPropertyMapper(),
                new TestCommandMapper(
                    new GrpcV1.CommandDescriptor[] { null! }),
                new TestEventMapper());

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreateInstrument() with
                        {
                            Interface =
                                new InstrumentInterface(
                                    commands:
                                        new[]
                                        {
                                            new CommandDescriptor(
                                                new DescriptorPath("Command"),
                                                "Command")
                                        })
                        }));

        Assert.Equal(
            "The Command descriptor mapper returned null.",
            exception.Message);
    }

    [Fact]
    public void Map_EventMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new InstrumentDescriptorMapper(
                new TestPropertyMapper(),
                new TestCommandMapper(),
                new TestEventMapper(
                    new GrpcV1.EventDescriptor[] { null! }));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreateInstrument() with
                        {
                            Interface =
                                new InstrumentInterface(
                                    events:
                                        new[]
                                        {
                                            new EventDescriptor(
                                                new DescriptorPath("Event"),
                                                "Event")
                                        })
                        }));

        Assert.Equal(
            "The Event descriptor mapper returned null.",
            exception.Message);
    }

    [Fact]
    public void Map_WithoutPresentation_ShouldLeaveTheDeclarationAbsent()
    {
        InstrumentDescriptorMapper mapper = CreateMapper();

        GrpcV1.InstrumentDescriptor mapped = mapper.Map(CreateInstrument());

        Assert.Null(mapped.Presentation);
    }

    [Fact]
    public void Map_DeclaredPanel_ShouldCarryThePanelIdentifier()
    {
        InstrumentDescriptorMapper mapper = CreateMapper();
        InstrumentDescriptor descriptor = CreateInstrument() with
        {
            Presentation = new InstrumentPresentation
            {
                PanelId = "rf-lab-signal-lab"
            }
        };

        GrpcV1.InstrumentDescriptor mapped = mapper.Map(descriptor);

        Assert.NotNull(mapped.Presentation);
        Assert.True(mapped.Presentation.HasPanelId);
        Assert.Equal("rf-lab-signal-lab", mapped.Presentation.PanelId);
    }

    [Fact]
    public void Map_PresentationWithoutPanel_ShouldCarryNoPanelIdentifier()
    {
        InstrumentDescriptorMapper mapper = CreateMapper();
        InstrumentDescriptor descriptor = CreateInstrument() with
        {
            Presentation = new InstrumentPresentation()
        };

        GrpcV1.InstrumentDescriptor mapped = mapper.Map(descriptor);

        Assert.NotNull(mapped.Presentation);
        Assert.False(mapped.Presentation.HasPanelId);
    }

    private static InstrumentDescriptorMapper CreateMapper()
    {
        return new InstrumentDescriptorMapper(
            new TestPropertyMapper(),
            new TestCommandMapper(),
            new TestEventMapper());
    }

    private static InstrumentDescriptor CreateInstrument()
    {
        return new InstrumentDescriptor(
            new InstrumentId(
                "instrument-1"),
            "Instrument 1",
            new InstrumentKind(
                "validation"));
    }

    private static PropertyDescriptor CreateProperty()
    {
        return new PropertyDescriptor(
            new PropertyId(
                "property-1"),
            new DescriptorPath(
                "Property"),
            "Property",
            new BooleanDataDescriptor());
    }

    private sealed class TestPropertyMapper
        : IPropertyDescriptorMapper
    {
        private readonly Queue<GrpcV1.PropertyDescriptor> results;

        public TestPropertyMapper(
            params GrpcV1.PropertyDescriptor[] results)
        {
            this.results =
                new Queue<GrpcV1.PropertyDescriptor>(
                    results);
        }

        public List<PropertyDescriptor> Inputs { get; } = new();

        public GrpcV1.PropertyDescriptor Map(
            PropertyDescriptor descriptor)
        {
            Inputs.Add(descriptor);
            return results.Dequeue();
        }
    }

    private sealed class TestCommandMapper
        : ICommandDescriptorMapper
    {
        private readonly Queue<GrpcV1.CommandDescriptor> results;

        public TestCommandMapper(
            params GrpcV1.CommandDescriptor[] results)
        {
            this.results =
                new Queue<GrpcV1.CommandDescriptor>(
                    results);
        }

        public List<CommandDescriptor> Inputs { get; } = new();

        public GrpcV1.CommandDescriptor Map(
            CommandDescriptor descriptor)
        {
            Inputs.Add(descriptor);
            return results.Dequeue();
        }
    }

    private sealed class TestEventMapper
        : IEventDescriptorMapper
    {
        private readonly Queue<GrpcV1.EventDescriptor> results;

        public TestEventMapper(
            params GrpcV1.EventDescriptor[] results)
        {
            this.results =
                new Queue<GrpcV1.EventDescriptor>(
                    results);
        }

        public List<EventDescriptor> Inputs { get; } = new();

        public GrpcV1.EventDescriptor Map(
            EventDescriptor descriptor)
        {
            Inputs.Add(descriptor);
            return results.Dequeue();
        }
    }
}
