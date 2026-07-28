using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class ByteArrayDataDescriptorSerializerTests
{
    [Fact]
    public void Write_ByteArrayDescriptor_WritesTypeDiscriminator()
    {
        DataDescriptorSerializer serializer =
            new();

        BinaryProtocolWriter writer =
            new();

        serializer.Write(
            writer,
            new ByteArrayDataDescriptor());

        Assert.Equal(
            new byte[]
            {
                0x04
            },
            writer.ToArray());
    }

    [Fact]
    public void Read_ByteArrayDescriptor_ReturnsByteArrayDescriptor()
    {
        DataDescriptorSerializer serializer =
            new();

        BinaryProtocolReader reader =
            new(
                new byte[]
                {
                    0x04
                });

        DataDescriptor descriptor =
            serializer.Read(
                reader);

        Assert.IsType<ByteArrayDataDescriptor>(
            descriptor);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_ByteArrayDescriptor_PreservesDescriptor()
    {
        DataDescriptorSerializer serializer =
            new();

        ByteArrayDataDescriptor original =
            new();

        BinaryProtocolWriter writer =
            new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(
                writer.ToArray());

        ByteArrayDataDescriptor decoded =
            Assert.IsType<ByteArrayDataDescriptor>(
                serializer.Read(
                    reader));

        Assert.Equal(
            original,
            decoded);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_ByteArrayProperty_PreservesDescriptor()
    {
        PropertyDescriptorSerializer serializer =
            new();

        PropertyDescriptor original =
            CreateByteArrayProperty();

        BinaryProtocolWriter writer =
            new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(
                writer.ToArray());

        PropertyDescriptor decoded =
            serializer.Read(
                reader);

        Assert.Equal(
            original,
            decoded);

        Assert.IsType<ByteArrayDataDescriptor>(
            decoded.Data);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_EndpointWithByteArrayProperty_PreservesNestedDescriptor()
    {
        EndpointDescriptorSerializer serializer =
            new();

        InstrumentDescriptor instrument =
            new(
                new InstrumentId(
                    "binary-controller"),
                "Binary Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        new[]
                        {
                            CreateByteArrayProperty()
                        },
                        Array.Empty<CommandDescriptor>(),
                        Array.Empty<EventDescriptor>())
            };

        EndpointDescriptor original =
            new(
                new EndpointId(
                    "binary-endpoint"),
                new[]
                {
                    instrument
                });

        BinaryProtocolWriter writer =
            new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(
                writer.ToArray());

        EndpointDescriptor decoded =
            serializer.Read(
                reader);

        PropertyDescriptor decodedProperty =
            Assert.Single(
                Assert.Single(
                    decoded.Instruments)
                    .Interface.Properties);

        Assert.IsType<ByteArrayDataDescriptor>(
            decodedProperty.Data);

        Assert.Equal(
            original.Instruments[0].Interface.Properties[0],
            decodedProperty);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    private static PropertyDescriptor CreateByteArrayProperty()
    {
        return new PropertyDescriptor(
            new PropertyId(
                "payload"),
            DescriptorPath.Parse(
                "Binary.Payload"),
            "Payload",
            new ByteArrayDataDescriptor())
        {
            Description =
                "Opaque application-defined bytes.",
            AccessMode =
                PropertyAccessMode.ReadWrite
        };
    }
}
