using Hase.Core.Domain.Instruments;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class InstrumentMetadataSerializerTests
{
    [Fact]
    public void Write_AllNullValues_WritesSixNullMarkers()
    {
        InstrumentMetadataSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            new InstrumentMetadata());

        Assert.Equal(
            new byte[]
            {
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00
            },
            writer.ToArray());
    }

    [Fact]
    public void Write_AllValues_WritesExpectedBytes()
    {
        InstrumentMetadataSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        InstrumentMetadata metadata = new()
        {
            Manufacturer = "EES",
            Model = "ENV1",
            SerialNumber = "123",
            FirmwareVersion = "1.0",
            HardwareRevision = "A",
            Description = "Sensor"
        };

        serializer.Write(writer, metadata);

        Assert.Equal(
            new byte[]
            {
                0x01,
                0x03, 0x00,
                (byte)'E', (byte)'E', (byte)'S',

                0x01,
                0x04, 0x00,
                (byte)'E', (byte)'N', (byte)'V', (byte)'1',

                0x01,
                0x03, 0x00,
                (byte)'1', (byte)'2', (byte)'3',

                0x01,
                0x03, 0x00,
                (byte)'1', (byte)'.', (byte)'0',

                0x01,
                0x01, 0x00,
                (byte)'A',

                0x01,
                0x06, 0x00,
                (byte)'S', (byte)'e', (byte)'n',
                (byte)'s', (byte)'o', (byte)'r'
            },
            writer.ToArray());
    }

    [Fact]
    public void Write_MixedValues_WritesCorrectMarkers()
    {
        InstrumentMetadataSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        InstrumentMetadata metadata = new()
        {
            Manufacturer = "EES",
            Model = null,
            SerialNumber = "123",
            FirmwareVersion = null,
            HardwareRevision = "A",
            Description = null
        };

        serializer.Write(writer, metadata);

        Assert.Equal(
            new byte[]
            {
                0x01,
                0x03, 0x00,
                (byte)'E', (byte)'E', (byte)'S',

                0x00,

                0x01,
                0x03, 0x00,
                (byte)'1', (byte)'2', (byte)'3',

                0x00,

                0x01,
                0x01, 0x00,
                (byte)'A',

                0x00
            },
            writer.ToArray());
    }

    [Fact]
    public void Read_AllNullMarkers_ReturnsNullValues()
    {
        InstrumentMetadataSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00
            });

        InstrumentMetadata metadata =
            serializer.Read(reader);

        Assert.Null(metadata.Manufacturer);
        Assert.Null(metadata.Model);
        Assert.Null(metadata.SerialNumber);
        Assert.Null(metadata.FirmwareVersion);
        Assert.Null(metadata.HardwareRevision);
        Assert.Null(metadata.Description);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void Read_AllValues_ReturnsExpectedMetadata()
    {
        InstrumentMetadataSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x01,
                0x03, 0x00,
                (byte)'E', (byte)'E', (byte)'S',

                0x01,
                0x04, 0x00,
                (byte)'E', (byte)'N', (byte)'V', (byte)'1',

                0x01,
                0x03, 0x00,
                (byte)'1', (byte)'2', (byte)'3',

                0x01,
                0x03, 0x00,
                (byte)'1', (byte)'.', (byte)'0',

                0x01,
                0x01, 0x00,
                (byte)'A',

                0x01,
                0x06, 0x00,
                (byte)'S', (byte)'e', (byte)'n',
                (byte)'s', (byte)'o', (byte)'r'
            });

        InstrumentMetadata metadata =
            serializer.Read(reader);

        Assert.Equal("EES", metadata.Manufacturer);
        Assert.Equal("ENV1", metadata.Model);
        Assert.Equal("123", metadata.SerialNumber);
        Assert.Equal("1.0", metadata.FirmwareVersion);
        Assert.Equal("A", metadata.HardwareRevision);
        Assert.Equal("Sensor", metadata.Description);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void RoundTrip_PreservesValues()
    {
        InstrumentMetadataSerializer serializer = new();

        InstrumentMetadata original = new()
        {
            Manufacturer = "EES Engineering",
            Model = "Environment Sensor",
            SerialNumber = "ENV-001",
            FirmwareVersion = "1.2.3",
            HardwareRevision = "B",
            Description = "Temperature and pressure instrument"
        };

        BinaryProtocolWriter writer = new();

        serializer.Write(writer, original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        InstrumentMetadata decoded =
            serializer.Read(reader);

        Assert.Equal(original, decoded);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void Read_InvalidPresenceMarker_ThrowsInvalidDataException()
    {
        InstrumentMetadataSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x02
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    [Fact]
    public void Read_TruncatedString_ThrowsInvalidDataException()
    {
        InstrumentMetadataSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x01,
                0x03, 0x00,
                (byte)'A', (byte)'B'
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }
}