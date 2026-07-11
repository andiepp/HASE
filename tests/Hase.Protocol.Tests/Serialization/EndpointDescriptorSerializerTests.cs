using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class EndpointDescriptorSerializerTests
{
    [Fact]
    public void Write_DefaultDescriptor_WritesExpectedBytes()
    {
        EndpointDescriptorSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        EndpointDescriptor descriptor =
            new(new EndpointId("endpoint"));

        serializer.Write(
            writer,
            descriptor);

        Assert.Equal(
            new byte[]
            {
                0x08, 0x00,
                (byte)'e', (byte)'n', (byte)'d', (byte)'p',
                (byte)'o', (byte)'i', (byte)'n', (byte)'t',

                // EndpointMetadata: DisplayName and Description are null.
                0x00,
                0x00,

                // No instruments.
                0x00, 0x00
            },
            writer.ToArray());
    }

    [Fact]
    public void Read_DefaultDescriptor_ReturnsExpectedValues()
    {
        EndpointDescriptorSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x08, 0x00,
                (byte)'e', (byte)'n', (byte)'d', (byte)'p',
                (byte)'o', (byte)'i', (byte)'n', (byte)'t',

                0x00,
                0x00,

                0x00, 0x00
            });

        EndpointDescriptor descriptor =
            serializer.Read(reader);

        Assert.Equal(
            new EndpointId("endpoint"),
            descriptor.Id);

        Assert.Equal(
            new EndpointMetadata(),
            descriptor.Metadata);

        Assert.Empty(
            descriptor.Instruments);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_WithMetadata_PreservesValues()
    {
        EndpointDescriptorSerializer serializer = new();

        EndpointDescriptor original =
            new(new EndpointId("lab-endpoint"))
            {
                Metadata = new EndpointMetadata
                {
                    DisplayName = "Laboratory Endpoint",
                    Description = "Controls laboratory instruments."
                }
            };

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        EndpointDescriptor decoded =
            serializer.Read(reader);

        Assert.Equal(
            original.Id,
            decoded.Id);

        Assert.Equal(
            original.Metadata,
            decoded.Metadata);

        Assert.Empty(
            decoded.Instruments);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_WithInstruments_PreservesValuesAndOrder()
    {
        EndpointDescriptorSerializer serializer = new();

        InstrumentDescriptor first = new(
            new InstrumentId("instrument-1"),
            "First Instrument",
            new InstrumentKind("sensor"));

        InstrumentDescriptor second = new(
            new InstrumentId("instrument-2"),
            "Second Instrument",
            new InstrumentKind("actuator"));

        EndpointDescriptor original = new(
            new EndpointId("endpoint"),
            new[] { first, second });

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        EndpointDescriptor decoded =
            serializer.Read(reader);

        Assert.Equal(
            original.Id,
            decoded.Id);

        Assert.Equal(
            original.Instruments,
            decoded.Instruments);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void Read_TruncatedPayload_ThrowsInvalidDataException()
    {
        EndpointDescriptorSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x08, 0x00,
                (byte)'e', (byte)'n'
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }
}
