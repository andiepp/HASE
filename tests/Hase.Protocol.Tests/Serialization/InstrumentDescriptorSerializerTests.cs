using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class InstrumentDescriptorSerializerTests
{
    [Fact]
    public void Write_DefaultDescriptor_WritesExpectedBytes()
    {
        InstrumentDescriptorSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        InstrumentDescriptor descriptor = new(
            new InstrumentId("sensor"),
            "Sensor",
            new InstrumentKind("environment"));

        serializer.Write(
            writer,
            descriptor);

        Assert.Equal(
            new byte[]
            {
                0x06, 0x00,
                (byte)'s', (byte)'e', (byte)'n',
                (byte)'s', (byte)'o', (byte)'r',

                0x06, 0x00,
                (byte)'S', (byte)'e', (byte)'n',
                (byte)'s', (byte)'o', (byte)'r',

                0x0B, 0x00,
                (byte)'e', (byte)'n', (byte)'v',
                (byte)'i', (byte)'r', (byte)'o',
                (byte)'n', (byte)'m', (byte)'e',
                (byte)'n', (byte)'t',

                // InstrumentMetadata: six null markers.
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,

                // InstrumentInterface: three empty collections.
                0x00, 0x00,
                0x00, 0x00,
                0x00, 0x00
            },
            writer.ToArray());
    }

    [Fact]
    public void Read_DefaultDescriptor_ReturnsExpectedValues()
    {
        InstrumentDescriptorSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x06, 0x00,
                (byte)'s', (byte)'e', (byte)'n',
                (byte)'s', (byte)'o', (byte)'r',

                0x06, 0x00,
                (byte)'S', (byte)'e', (byte)'n',
                (byte)'s', (byte)'o', (byte)'r',

                0x0B, 0x00,
                (byte)'e', (byte)'n', (byte)'v',
                (byte)'i', (byte)'r', (byte)'o',
                (byte)'n', (byte)'m', (byte)'e',
                (byte)'n', (byte)'t',

                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,

                0x00, 0x00,
                0x00, 0x00,
                0x00, 0x00
            });

        InstrumentDescriptor descriptor =
            serializer.Read(reader);

        Assert.Equal(
            new InstrumentId("sensor"),
            descriptor.Id);

        Assert.Equal(
            "Sensor",
            descriptor.Name);

        Assert.Equal(
            new InstrumentKind("environment"),
            descriptor.Kind);

        Assert.Equal(
            new InstrumentMetadata(),
            descriptor.Metadata);

        Assert.Empty(
            descriptor.Interface.Properties);

        Assert.Empty(
            descriptor.Interface.Commands);

        Assert.Empty(
            descriptor.Interface.Events);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_WithMetadata_PreservesValues()
    {
        InstrumentDescriptorSerializer serializer = new();

        InstrumentDescriptor original = new(
            new InstrumentId("environment-sensor"),
            "Environment Sensor",
            new InstrumentKind("environment"))
        {
            Metadata = new InstrumentMetadata
            {
                Manufacturer = "EES Engineering",
                Model = "ENV-1",
                SerialNumber = "12345",
                FirmwareVersion = "1.2.3",
                HardwareRevision = "B",
                Description = "Temperature and pressure sensor."
            }
        };

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        InstrumentDescriptor decoded =
            serializer.Read(reader);

        Assert.Equal(
            original.Id,
            decoded.Id);

        Assert.Equal(
            original.Name,
            decoded.Name);

        Assert.Equal(
            original.Kind,
            decoded.Kind);

        Assert.Equal(
            original.Metadata,
            decoded.Metadata);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_WithInterface_PreservesValues()
    {
        InstrumentDescriptorSerializer serializer = new();

        PropertyDescriptor property = new(
            new PropertyId("temperature"),
            DescriptorPath.Parse("Environment.Temperature"),
            "Temperature",
            new StringDataDescriptor())
        {
            AccessMode = PropertyAccessMode.Read
        };

        CommandDescriptor command = new(
            DescriptorPath.Parse("Environment.Reset"),
            "Reset");

        EventDescriptor eventDescriptor = new(
            DescriptorPath.Parse("Environment.Alarm"),
            "Alarm");

        InstrumentDescriptor original = new(
            new InstrumentId("environment-sensor"),
            "Environment Sensor",
            new InstrumentKind("environment"))
        {
            Interface = new InstrumentInterface(
                new[] { property },
                new[] { command },
                new[] { eventDescriptor })
        };

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        InstrumentDescriptor decoded =
            serializer.Read(reader);

        Assert.Equal(
            original.Interface.Properties,
            decoded.Interface.Properties);

        Assert.Equal(
            original.Interface.Commands,
            decoded.Interface.Commands);

        Assert.Equal(
            original.Interface.Events,
            decoded.Interface.Events);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void Read_TruncatedPayload_ThrowsInvalidDataException()
    {
        InstrumentDescriptorSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x06, 0x00,
                (byte)'s', (byte)'e'
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }
}