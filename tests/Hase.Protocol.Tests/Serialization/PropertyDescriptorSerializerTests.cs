using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class PropertyDescriptorSerializerTests
{
    [Fact]
    public void Write_StringPropertyWithoutDescription_WritesExpectedBytes()
    {
        PropertyDescriptorSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        PropertyDescriptor descriptor = new(
            new PropertyId("name"),
            DescriptorPath.Parse("Device.Name"),
            "Name",
            new StringDataDescriptor())
        {
            AccessMode = PropertyAccessMode.Read
        };

        serializer.Write(
            writer,
            descriptor);

        Assert.Equal(
            new byte[]
            {
                0x04, 0x00,
                (byte)'n', (byte)'a',
                (byte)'m', (byte)'e',

                0x0B, 0x00,
                (byte)'D', (byte)'e',
                (byte)'v', (byte)'i',
                (byte)'c', (byte)'e',
                (byte)'.',
                (byte)'N', (byte)'a',
                (byte)'m', (byte)'e',

                0x04, 0x00,
                (byte)'N', (byte)'a',
                (byte)'m', (byte)'e',

                0x00,

                0x01,

                0x01
            },
            writer.ToArray());
    }

    [Fact]
    public void Read_StringPropertyWithoutDescription_ReturnsExpectedDescriptor()
    {
        PropertyDescriptorSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x04, 0x00,
                (byte)'n', (byte)'a',
                (byte)'m', (byte)'e',

                0x0B, 0x00,
                (byte)'D', (byte)'e',
                (byte)'v', (byte)'i',
                (byte)'c', (byte)'e',
                (byte)'.',
                (byte)'N', (byte)'a',
                (byte)'m', (byte)'e',

                0x04, 0x00,
                (byte)'N', (byte)'a',
                (byte)'m', (byte)'e',

                0x00,

                0x01,

                0x01
            });

        PropertyDescriptor descriptor =
            serializer.Read(reader);

        Assert.Equal(
            new PropertyId("name"),
            descriptor.Id);

        Assert.Equal(
            DescriptorPath.Parse("Device.Name"),
            descriptor.Path);

        Assert.Equal(
            "Name",
            descriptor.DisplayName);

        Assert.Null(
            descriptor.Description);

        Assert.Equal(
            PropertyAccessMode.Read,
            descriptor.AccessMode);

        Assert.IsType<StringDataDescriptor>(
            descriptor.Data);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_StringProperty_PreservesValues()
    {
        PropertyDescriptorSerializer serializer = new();

        PropertyDescriptor original = new(
            new PropertyId("device-name"),
            DescriptorPath.Parse("Device.Name"),
            "Device Name",
            new StringDataDescriptor())
        {
            Description = "User-visible device name.",
            AccessMode = PropertyAccessMode.ReadWrite
        };

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        PropertyDescriptor decoded =
            serializer.Read(reader);

        Assert.Equal(
            original,
            decoded);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_NumericProperty_PreservesValues()
    {
        PropertyDescriptorSerializer serializer = new();

        Quantity quantity = new(
            "temperature",
            "Temperature");

        Unit unit = new(
            "degC",
            "Degrees Celsius",
            "°C",
            quantity);

        PropertyDescriptor original = new(
            new PropertyId("temperature"),
            DescriptorPath.Parse("Environment.Temperature"),
            "Temperature",
            new NumericDataDescriptor(
                quantity,
                unit,
                new ValueRange(-40.0, 85.0),
                new Resolution(0.1)))
        {
            Description = "Measured ambient temperature.",
            AccessMode = PropertyAccessMode.Read
        };

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        PropertyDescriptor decoded =
            serializer.Read(reader);

        Assert.Equal(
            original,
            decoded);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Theory]
    [InlineData(PropertyAccessMode.None)]
    [InlineData(PropertyAccessMode.Read)]
    [InlineData(PropertyAccessMode.Write)]
    [InlineData(PropertyAccessMode.ReadWrite)]
    public void RoundTrip_PreservesAccessMode(
        PropertyAccessMode accessMode)
    {
        PropertyDescriptorSerializer serializer = new();

        PropertyDescriptor original = new(
            new PropertyId("value"),
            DescriptorPath.Parse("Device.Value"),
            "Value",
            new StringDataDescriptor())
        {
            AccessMode = accessMode
        };

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        PropertyDescriptor decoded =
            serializer.Read(reader);

        Assert.Equal(
            accessMode,
            decoded.AccessMode);
    }

    [Fact]
    public void Read_UnknownAccessMode_ThrowsInvalidDataException()
    {
        PropertyDescriptorSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        writer.WriteString("value");
        writer.WriteString("Device.Value");
        writer.WriteString("Value");

        ProtocolSerializationHelper.WriteOptionalString(
            writer,
            null);

        writer.WriteByte(0xFF);
        writer.WriteByte(0x01);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    [Fact]
    public void Read_UnknownDataDescriptorType_ThrowsInvalidDataException()
    {
        PropertyDescriptorSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        writer.WriteString("value");
        writer.WriteString("Device.Value");
        writer.WriteString("Value");

        ProtocolSerializationHelper.WriteOptionalString(
            writer,
            null);

        writer.WriteByte(
            (byte)PropertyAccessMode.Read);

        writer.WriteByte(0xFF);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    [Fact]
    public void Read_TruncatedPayload_ThrowsInvalidDataException()
    {
        PropertyDescriptorSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x05, 0x00,
                (byte)'v', (byte)'a'
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }
}
