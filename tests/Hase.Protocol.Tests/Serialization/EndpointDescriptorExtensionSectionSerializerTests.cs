using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class EndpointDescriptorExtensionSectionSerializerTests
{
    [Fact]
    public void Write_OneExtension_WritesExpectedBytes()
    {
        EndpointDescriptorExtensionSectionSerializer serializer =
            new();

        BinaryProtocolWriter writer =
            new();

        serializer.Write(
            writer,
            new[]
            {
                new EndpointDescriptorExtension(
                    0x01,
                    new byte[]
                    {
                        0xAA,
                        0xBB
                    })
            });

        Assert.Equal(
            new byte[]
            {
                0x01, 0x00,
                0x01,
                0x02, 0x00,
                0xAA, 0xBB
            },
            writer.ToArray());
    }

    [Fact]
    public void RoundTrip_MultipleExtensions_PreservesOrderTypeAndPayload()
    {
        EndpointDescriptorExtensionSectionSerializer serializer =
            new();

        EndpointDescriptorExtension[] original =
        {
            new(
                0x01,
                new byte[]
                {
                    0x10
                }),
            new(
                0xFE,
                new byte[]
                {
                    0x00,
                    0x7F,
                    0xFF
                })
        };

        BinaryProtocolWriter writer =
            new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(
                writer.ToArray());

        IReadOnlyList<EndpointDescriptorExtension> decoded =
            serializer.Read(
                reader);

        Assert.Equal(
            2,
            decoded.Count);

        Assert.Equal(
            0x01,
            decoded[0].Type);

        Assert.True(
            decoded[0].Payload.SequenceEqual(
                new byte[]
                {
                    0x10
                }));

        Assert.Equal(
            0xFE,
            decoded[1].Type);

        Assert.True(
            decoded[1].Payload.SequenceEqual(
                new byte[]
                {
                    0x00,
                    0x7F,
                    0xFF
                }));

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void Write_EmptySection_ThrowsArgumentException()
    {
        EndpointDescriptorExtensionSectionSerializer serializer =
            new();

        Assert.Throws<ArgumentException>(
            () => serializer.Write(
                new BinaryProtocolWriter(),
                Array.Empty<EndpointDescriptorExtension>()));
    }

    [Fact]
    public void Constructor_EmptyPayload_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new EndpointDescriptorExtension(
                0x01,
                Array.Empty<byte>()));
    }

    [Fact]
    public void Write_OversizedPayload_ThrowsArgumentOutOfRangeException()
    {
        EndpointDescriptorExtensionSectionSerializer serializer =
            new();

        EndpointDescriptorExtension extension =
            new(
                0x01,
                new byte[ushort.MaxValue + 1]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => serializer.Write(
                new BinaryProtocolWriter(),
                new[]
                {
                    extension
                }));
    }

    [Fact]
    public void Read_ZeroExtensionCount_ThrowsInvalidDataException()
    {
        EndpointDescriptorExtensionSectionSerializer serializer =
            new();

        BinaryProtocolReader reader =
            new(
                new byte[]
                {
                    0x00, 0x00
                });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(
                reader));
    }

    [Fact]
    public void Read_EmptyPayload_ThrowsInvalidDataException()
    {
        EndpointDescriptorExtensionSectionSerializer serializer =
            new();

        BinaryProtocolReader reader =
            new(
                new byte[]
                {
                    0x01, 0x00,
                    0x01,
                    0x00, 0x00
                });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(
                reader));
    }

    [Fact]
    public void Read_TruncatedPayload_ThrowsInvalidDataException()
    {
        EndpointDescriptorExtensionSectionSerializer serializer =
            new();

        BinaryProtocolReader reader =
            new(
                new byte[]
                {
                    0x01, 0x00,
                    0x01,
                    0x03, 0x00,
                    0xAA, 0xBB
                });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(
                reader));
    }
}
