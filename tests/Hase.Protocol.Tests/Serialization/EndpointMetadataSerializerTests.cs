using Hase.Core.Domain.Endpoints;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class EndpointMetadataSerializerTests
{
    [Fact]
    public void Write_NullValues_WritesNullMarkers()
    {
        EndpointMetadataSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            new EndpointMetadata());

        Assert.Equal(
            new byte[]
            {
                0x00,
                0x00
            },
            writer.ToArray());
    }

    [Fact]
    public void Write_Values_WritesPresenceMarkersAndStrings()
    {
        EndpointMetadataSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        EndpointMetadata metadata = new()
        {
            DisplayName = "Lab",
            Description = "Main endpoint"
        };

        serializer.Write(
            writer,
            metadata);

        Assert.Equal(
            new byte[]
            {
                0x01,
                0x03, 0x00,
                (byte)'L', (byte)'a', (byte)'b',

                0x01,
                0x0D, 0x00,
                (byte)'M', (byte)'a', (byte)'i', (byte)'n',
                (byte)' ', (byte)'e', (byte)'n', (byte)'d',
                (byte)'p', (byte)'o', (byte)'i', (byte)'n',
                (byte)'t'
            },
            writer.ToArray());
    }

    [Fact]
    public void Write_MixedValues_WritesCorrectMarkers()
    {
        EndpointMetadataSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        EndpointMetadata metadata = new()
        {
            DisplayName = "Lab",
            Description = null
        };

        serializer.Write(
            writer,
            metadata);

        Assert.Equal(
            new byte[]
            {
                0x01,
                0x03, 0x00,
                (byte)'L', (byte)'a', (byte)'b',

                0x00
            },
            writer.ToArray());
    }

    [Fact]
    public void Read_NullMarkers_ReturnsNullValues()
    {
        EndpointMetadataSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x00,
                0x00
            });

        EndpointMetadata metadata =
            serializer.Read(reader);

        Assert.Null(
            metadata.DisplayName);

        Assert.Null(
            metadata.Description);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void Read_Values_ReturnsExpectedMetadata()
    {
        EndpointMetadataSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x01,
                0x03, 0x00,
                (byte)'L', (byte)'a', (byte)'b',

                0x01,
                0x04, 0x00,
                (byte)'T', (byte)'e', (byte)'s', (byte)'t'
            });

        EndpointMetadata metadata =
            serializer.Read(reader);

        Assert.Equal(
            "Lab",
            metadata.DisplayName);

        Assert.Equal(
            "Test",
            metadata.Description);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_PreservesValues()
    {
        EndpointMetadataSerializer serializer = new();

        EndpointMetadata original = new()
        {
            DisplayName = "Environment endpoint",
            Description = "Temperature and pressure sensors"
        };

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        EndpointMetadata decoded =
            serializer.Read(reader);

        Assert.Equal(
            original,
            decoded);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void Read_InvalidPresenceMarker_ThrowsInvalidDataException()
    {
        EndpointMetadataSerializer serializer = new();

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
        EndpointMetadataSerializer serializer = new();

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