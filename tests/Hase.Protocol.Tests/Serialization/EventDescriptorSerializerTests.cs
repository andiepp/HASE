using Hase.Core.Domain.Events;
using Hase.Core.Domain.Properties;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class EventDescriptorSerializerTests
{
    [Fact]
    public void Write_EventWithoutDescription_WritesExpectedBytes()
    {
        EventDescriptorSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        EventDescriptor descriptor = new(
            DescriptorPath.Parse("DDS.LockLost"),
            "PLL Lock Lost");

        serializer.Write(
            writer,
            descriptor);

        Assert.Equal(
            new byte[]
            {
                0x0C,0x00,
                (byte)'D',(byte)'D',(byte)'S',
                (byte)'.',
                (byte)'L',(byte)'o',(byte)'c',(byte)'k',
                (byte)'L',(byte)'o',(byte)'s',(byte)'t',

                0x0D,0x00,
                (byte)'P',(byte)'L',(byte)'L',
                (byte)' ',
                (byte)'L',(byte)'o',(byte)'c',(byte)'k',
                (byte)' ',
                (byte)'L',(byte)'o',(byte)'s',(byte)'t',

                0x00
            },
            writer.ToArray());
    }

    [Fact]
    public void Write_EventWithDescription_WritesExpectedBytes()
    {
        EventDescriptorSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        EventDescriptor descriptor = new(
            DescriptorPath.Parse("DDS.LockLost"),
            "PLL Lock Lost")
        {
            Description = "PLL synchronization lost."
        };

        serializer.Write(
            writer,
            descriptor);

        Assert.Equal(
            new byte[]
            {
            0x0C, 0x00,
            (byte)'D', (byte)'D', (byte)'S',
            (byte)'.',
            (byte)'L', (byte)'o', (byte)'c', (byte)'k',
            (byte)'L', (byte)'o', (byte)'s', (byte)'t',

            0x0D, 0x00,
            (byte)'P', (byte)'L', (byte)'L',
            (byte)' ',
            (byte)'L', (byte)'o', (byte)'c', (byte)'k',
            (byte)' ',
            (byte)'L', (byte)'o', (byte)'s', (byte)'t',

            0x01,

            0x19, 0x00,
            (byte)'P', (byte)'L', (byte)'L',
            (byte)' ',
            (byte)'s', (byte)'y', (byte)'n',
            (byte)'c', (byte)'h', (byte)'r',
            (byte)'o', (byte)'n', (byte)'i',
            (byte)'z', (byte)'a', (byte)'t',
            (byte)'i', (byte)'o', (byte)'n',
            (byte)' ',
            (byte)'l', (byte)'o', (byte)'s',
            (byte)'t', (byte)'.'
            },
            writer.ToArray());
    }

    [Fact]
    public void Read_EventWithoutDescription_ReturnsExpectedDescriptor()
    {
        EventDescriptorSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        EventDescriptor original = new(
            DescriptorPath.Parse("DDS.LockLost"),
            "PLL Lock Lost");

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        EventDescriptor decoded =
            serializer.Read(reader);

        Assert.Equal(
            original,
            decoded);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void Read_EventWithDescription_ReturnsExpectedDescriptor()
    {
        EventDescriptorSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        EventDescriptor original = new(
            DescriptorPath.Parse("DDS.LockLost"),
            "PLL Lock Lost")
        {
            Description = "PLL synchronization lost."
        };

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        EventDescriptor decoded =
            serializer.Read(reader);

        Assert.Equal(
            original,
            decoded);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void Read_InvalidDescriptionMarker_ThrowsInvalidDataException()
    {
        EventDescriptorSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        writer.WriteString("DDS.LockLost");
        writer.WriteString("PLL Lock Lost");
        writer.WriteByte(0x02);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    [Fact]
    public void Read_TruncatedPayload_ThrowsInvalidDataException()
    {
        EventDescriptorSerializer serializer = new();

        BinaryProtocolReader reader =
            new(new byte[]
            {
                0x04,0x00,
                (byte)'D'
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    [Fact]
    public void RoundTrip_PreservesValues()
    {
        EventDescriptorSerializer serializer = new();

        EventDescriptor original = new(
            DescriptorPath.Parse("DDS.PLL.LockLost"),
            "PLL Lock Lost")
        {
            Description = "PLL synchronization lost."
        };

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        EventDescriptor decoded =
            serializer.Read(reader);

        Assert.Equal(
            original,
            decoded);

        Assert.Equal(
            0,
            reader.Remaining);
    }
}