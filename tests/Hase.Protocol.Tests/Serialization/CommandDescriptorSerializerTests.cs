using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Properties;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class CommandDescriptorSerializerTests
{
    [Fact]
    public void Write_DescriptorWithoutDescription_WritesExpectedBytes()
    {
        CommandDescriptorSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        CommandDescriptor descriptor = new(
            DescriptorPath.Parse("DDS.Reset"),
            "Reset");

        serializer.Write(
            writer,
            descriptor);

        Assert.Equal(
            new byte[]
            {
                0x09, 0x00,
                (byte)'D', (byte)'D', (byte)'S',
                (byte)'.',
                (byte)'R', (byte)'e', (byte)'s',
                (byte)'e', (byte)'t',

                0x05, 0x00,
                (byte)'R', (byte)'e', (byte)'s',
                (byte)'e', (byte)'t',

                0x00
            },
            writer.ToArray());
    }

    [Fact]
    public void Write_DescriptorWithDescription_WritesExpectedBytes()
    {
        CommandDescriptorSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        CommandDescriptor descriptor = new(
            DescriptorPath.Parse("DDS.Reset"),
            "Reset")
        {
            Description = "Restart"
        };

        serializer.Write(
            writer,
            descriptor);

        Assert.Equal(
            new byte[]
            {
                0x09, 0x00,
                (byte)'D', (byte)'D', (byte)'S',
                (byte)'.',
                (byte)'R', (byte)'e', (byte)'s',
                (byte)'e', (byte)'t',

                0x05, 0x00,
                (byte)'R', (byte)'e', (byte)'s',
                (byte)'e', (byte)'t',

                0x01,
                0x07, 0x00,
                (byte)'R', (byte)'e', (byte)'s',
                (byte)'t', (byte)'a', (byte)'r',
                (byte)'t'
            },
            writer.ToArray());
    }

    [Fact]
    public void Read_DescriptorWithoutDescription_ReturnsExpectedDescriptor()
    {
        CommandDescriptorSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x09, 0x00,
                (byte)'D', (byte)'D', (byte)'S',
                (byte)'.',
                (byte)'R', (byte)'e', (byte)'s',
                (byte)'e', (byte)'t',

                0x05, 0x00,
                (byte)'R', (byte)'e', (byte)'s',
                (byte)'e', (byte)'t',

                0x00
            });

        CommandDescriptor descriptor =
            serializer.Read(reader);

        Assert.Equal(
            DescriptorPath.Parse("DDS.Reset"),
            descriptor.Path);

        Assert.Equal(
            "Reset",
            descriptor.DisplayName);

        Assert.Null(
            descriptor.Description);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void Read_DescriptorWithDescription_ReturnsExpectedDescriptor()
    {
        CommandDescriptorSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x09, 0x00,
                (byte)'D', (byte)'D', (byte)'S',
                (byte)'.',
                (byte)'R', (byte)'e', (byte)'s',
                (byte)'e', (byte)'t',

                0x05, 0x00,
                (byte)'R', (byte)'e', (byte)'s',
                (byte)'e', (byte)'t',

                0x01,
                0x07, 0x00,
                (byte)'R', (byte)'e', (byte)'s',
                (byte)'t', (byte)'a', (byte)'r',
                (byte)'t'
            });

        CommandDescriptor descriptor =
            serializer.Read(reader);

        Assert.Equal(
            DescriptorPath.Parse("DDS.Reset"),
            descriptor.Path);

        Assert.Equal(
            "Reset",
            descriptor.DisplayName);

        Assert.Equal(
            "Restart",
            descriptor.Description);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_PreservesValues()
    {
        CommandDescriptorSerializer serializer = new();

        CommandDescriptor original = new(
            DescriptorPath.Parse("DDS.Sweep.Start"),
            "Start Sweep")
        {
            Description = "Starts the configured frequency sweep."
        };

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        CommandDescriptor decoded =
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
        CommandDescriptorSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        writer.WriteString("DDS.Reset");
        writer.WriteString("Reset");
        writer.WriteByte(0x02);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    [Fact]
    public void Read_TruncatedPath_ThrowsInvalidDataException()
    {
        CommandDescriptorSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x09, 0x00,
                (byte)'D', (byte)'D', (byte)'S'
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }
}