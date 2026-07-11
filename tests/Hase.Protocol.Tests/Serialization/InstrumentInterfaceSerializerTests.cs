using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class InstrumentInterfaceSerializerTests
{
    [Fact]
    public void Write_EmptyInterface_WritesThreeZeroCounts()
    {
        InstrumentInterfaceSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            new InstrumentInterface());

        Assert.Equal(
            new byte[]
            {
                0x00, 0x00,
                0x00, 0x00,
                0x00, 0x00
            },
            writer.ToArray());
    }

    [Fact]
    public void Read_EmptyInterface_ReturnsEmptyCollections()
    {
        InstrumentInterfaceSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x00, 0x00,
                0x00, 0x00,
                0x00, 0x00
            });

        InstrumentInterface instrumentInterface =
            serializer.Read(reader);

        Assert.Empty(
            instrumentInterface.Properties);

        Assert.Empty(
            instrumentInterface.Commands);

        Assert.Empty(
            instrumentInterface.Events);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_WithAllDescriptorTypes_PreservesValues()
    {
        InstrumentInterfaceSerializer serializer = new();

        PropertyDescriptor property = new(
            new PropertyId("device-name"),
            DescriptorPath.Parse("Device.Name"),
            "Device Name",
            new StringDataDescriptor())
        {
            Description = "User-visible device name.",
            AccessMode = PropertyAccessMode.ReadWrite
        };

        CommandDescriptor command = new(
            DescriptorPath.Parse("Device.Reset"),
            "Reset")
        {
            Description = "Restarts the device."
        };

        EventDescriptor eventDescriptor = new(
            DescriptorPath.Parse("Device.Restarted"),
            "Restarted")
        {
            Description = "Raised after a successful restart."
        };

        InstrumentInterface original = new(
            new[] { property },
            new[] { command },
            new[] { eventDescriptor });

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        InstrumentInterface decoded =
            serializer.Read(reader);

        Assert.Equal(
            original.Properties,
            decoded.Properties);

        Assert.Equal(
            original.Commands,
            decoded.Commands);

        Assert.Equal(
            original.Events,
            decoded.Events);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_WithMultipleDescriptors_PreservesOrder()
    {
        InstrumentInterfaceSerializer serializer = new();

        PropertyDescriptor firstProperty = new(
            new PropertyId("first"),
            DescriptorPath.Parse("Device.First"),
            "First",
            new StringDataDescriptor());

        PropertyDescriptor secondProperty = new(
            new PropertyId("second"),
            DescriptorPath.Parse("Device.Second"),
            "Second",
            new StringDataDescriptor());

        CommandDescriptor firstCommand = new(
            DescriptorPath.Parse("Device.Start"),
            "Start");

        CommandDescriptor secondCommand = new(
            DescriptorPath.Parse("Device.Stop"),
            "Stop");

        EventDescriptor firstEvent = new(
            DescriptorPath.Parse("Device.Started"),
            "Started");

        EventDescriptor secondEvent = new(
            DescriptorPath.Parse("Device.Stopped"),
            "Stopped");

        InstrumentInterface original = new(
            new[] { firstProperty, secondProperty },
            new[] { firstCommand, secondCommand },
            new[] { firstEvent, secondEvent });

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        InstrumentInterface decoded =
            serializer.Read(reader);

        Assert.Equal(
            original.Properties,
            decoded.Properties);

        Assert.Equal(
            original.Commands,
            decoded.Commands);

        Assert.Equal(
            original.Events,
            decoded.Events);
    }

    [Fact]
    public void Read_TruncatedPropertyCollection_ThrowsInvalidDataException()
    {
        InstrumentInterfaceSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x01, 0x00
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    [Fact]
    public void Read_TruncatedCommandCollection_ThrowsInvalidDataException()
    {
        InstrumentInterfaceSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x00, 0x00,
                0x01, 0x00
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    [Fact]
    public void Read_TruncatedEventCollection_ThrowsInvalidDataException()
    {
        InstrumentInterfaceSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x00, 0x00,
                0x00, 0x00,
                0x01, 0x00
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }
}
