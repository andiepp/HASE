using Hase.Core.Domain.Data;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class DataDescriptorSerializerTests
{
    private sealed record UnsupportedDataDescriptor
        : DataDescriptor;

    [Fact]
    public void Write_StringDescriptor_WritesTypeDiscriminator()
    {
        DataDescriptorSerializer serializer =
            new();

        BinaryProtocolWriter writer =
            new();

        serializer.Write(
            writer,
            new StringDataDescriptor());

        Assert.Equal(
            new byte[]
            {
                0x01
            },
            writer.ToArray());
    }

    [Fact]
    public void Read_StringDescriptor_ReturnsStringDescriptor()
    {
        DataDescriptorSerializer serializer =
            new();

        BinaryProtocolReader reader =
            new(
                new byte[]
                {
                    0x01
                });

        DataDescriptor descriptor =
            serializer.Read(
                reader);

        Assert.IsType<StringDataDescriptor>(
            descriptor);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void Write_BooleanDescriptor_WritesTypeDiscriminator()
    {
        DataDescriptorSerializer serializer =
            new();

        BinaryProtocolWriter writer =
            new();

        serializer.Write(
            writer,
            new BooleanDataDescriptor());

        Assert.Equal(
            new byte[]
            {
                0x03
            },
            writer.ToArray());
    }

    [Fact]
    public void Read_BooleanDescriptor_ReturnsBooleanDescriptor()
    {
        DataDescriptorSerializer serializer =
            new();

        BinaryProtocolReader reader =
            new(
                new byte[]
                {
                    0x03
                });

        DataDescriptor descriptor =
            serializer.Read(
                reader);

        Assert.IsType<BooleanDataDescriptor>(
            descriptor);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_BooleanDescriptor_PreservesDescriptor()
    {
        DataDescriptorSerializer serializer =
            new();

        var original =
            new BooleanDataDescriptor();

        BinaryProtocolWriter writer =
            new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(
                writer.ToArray());

        BooleanDataDescriptor decoded =
            Assert.IsType<BooleanDataDescriptor>(
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
    public void Write_NumericDescriptorWithoutOptionalValues_WritesExpectedBytes()
    {
        DataDescriptorSerializer serializer =
            new();

        BinaryProtocolWriter writer =
            new();

        Quantity quantity =
            new(
                "temp",
                "Temperature");

        Unit unit =
            new(
                "degC",
                "Celsius",
                "C",
                quantity);

        NumericDataDescriptor descriptor =
            new(
                quantity,
                unit);

        serializer.Write(
            writer,
            descriptor);

        Assert.Equal(
            new byte[]
            {
                0x02,

                0x04, 0x00,
                (byte)'t', (byte)'e',
                (byte)'m', (byte)'p',

                0x0B, 0x00,
                (byte)'T', (byte)'e',
                (byte)'m', (byte)'p',
                (byte)'e', (byte)'r',
                (byte)'a', (byte)'t',
                (byte)'u', (byte)'r',
                (byte)'e',

                0x04, 0x00,
                (byte)'d', (byte)'e',
                (byte)'g', (byte)'C',

                0x07, 0x00,
                (byte)'C', (byte)'e',
                (byte)'l', (byte)'s',
                (byte)'i', (byte)'u',
                (byte)'s',

                0x01, 0x00,
                (byte)'C',

                0x00,
                0x00
            },
            writer.ToArray());
    }

    [Fact]
    public void Read_NumericDescriptorWithoutOptionalValues_ReturnsExpectedDescriptor()
    {
        DataDescriptorSerializer serializer =
            new();

        BinaryProtocolReader reader =
            new(
                new byte[]
                {
                    0x02,

                    0x04, 0x00,
                    (byte)'t', (byte)'e',
                    (byte)'m', (byte)'p',

                    0x0B, 0x00,
                    (byte)'T', (byte)'e',
                    (byte)'m', (byte)'p',
                    (byte)'e', (byte)'r',
                    (byte)'a', (byte)'t',
                    (byte)'u', (byte)'r',
                    (byte)'e',

                    0x04, 0x00,
                    (byte)'d', (byte)'e',
                    (byte)'g', (byte)'C',

                    0x07, 0x00,
                    (byte)'C', (byte)'e',
                    (byte)'l', (byte)'s',
                    (byte)'i', (byte)'u',
                    (byte)'s',

                    0x01, 0x00,
                    (byte)'C',

                    0x00,
                    0x00
                });

        NumericDataDescriptor descriptor =
            Assert.IsType<NumericDataDescriptor>(
                serializer.Read(
                    reader));

        Assert.Equal(
            new Quantity(
                "temp",
                "Temperature"),
            descriptor.Quantity);

        Assert.Equal(
            "degC",
            descriptor.NativeUnit.Id);

        Assert.Equal(
            "Celsius",
            descriptor.NativeUnit.DisplayName);

        Assert.Equal(
            "C",
            descriptor.NativeUnit.Symbol);

        Assert.Equal(
            descriptor.Quantity,
            descriptor.NativeUnit.Quantity);

        Assert.Null(
            descriptor.Range);

        Assert.Null(
            descriptor.Resolution);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void RoundTrip_NumericDescriptorWithRangeAndResolution_PreservesValues()
    {
        DataDescriptorSerializer serializer =
            new();

        Quantity quantity =
            new(
                "pressure",
                "Pressure");

        Unit unit =
            new(
                "hPa",
                "Hectopascal",
                "hPa",
                quantity);

        NumericDataDescriptor original =
            new(
                quantity,
                unit,
                new ValueRange(
                    300.0,
                    1100.0),
                new Resolution(
                    0.1));

        BinaryProtocolWriter writer =
            new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(
                writer.ToArray());

        NumericDataDescriptor decoded =
            Assert.IsType<NumericDataDescriptor>(
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
    public void Write_UnsupportedDescriptor_ThrowsNotSupportedException()
    {
        DataDescriptorSerializer serializer =
            new();

        BinaryProtocolWriter writer =
            new();

        Assert.Throws<
            NotSupportedException>(
                () =>
                    serializer.Write(
                        writer,
                        new UnsupportedDataDescriptor()));
    }

    [Fact]
    public void Read_UnknownTypeDiscriminator_ThrowsInvalidDataException()
    {
        DataDescriptorSerializer serializer =
            new();

        BinaryProtocolReader reader =
            new(
                new byte[]
                {
                    0xFF
                });

        Assert.Throws<
            InvalidDataException>(
                () =>
                    serializer.Read(
                        reader));
    }

    [Fact]
    public void Read_InvalidRangeMarker_ThrowsInvalidDataException()
    {
        DataDescriptorSerializer serializer =
            new();

        BinaryProtocolWriter writer =
            new();

        WriteNumericDescriptorHeader(
            writer);

        writer.WriteByte(
            0x02);

        BinaryProtocolReader reader =
            new(
                writer.ToArray());

        Assert.Throws<
            InvalidDataException>(
                () =>
                    serializer.Read(
                        reader));
    }

    [Fact]
    public void Read_InvalidResolutionMarker_ThrowsInvalidDataException()
    {
        DataDescriptorSerializer serializer =
            new();

        BinaryProtocolWriter writer =
            new();

        WriteNumericDescriptorHeader(
            writer);

        writer.WriteByte(
            0x00);

        writer.WriteByte(
            0x02);

        BinaryProtocolReader reader =
            new(
                writer.ToArray());

        Assert.Throws<
            InvalidDataException>(
                () =>
                    serializer.Read(
                        reader));
    }

    [Fact]
    public void Read_TruncatedNumericDescriptor_ThrowsInvalidDataException()
    {
        DataDescriptorSerializer serializer =
            new();

        BinaryProtocolReader reader =
            new(
                new byte[]
                {
                    0x02,
                    0x04, 0x00,
                    (byte)'t', (byte)'e'
                });

        Assert.Throws<
            InvalidDataException>(
                () =>
                    serializer.Read(
                        reader));
    }

    private static void WriteNumericDescriptorHeader(
        BinaryProtocolWriter writer)
    {
        writer.WriteByte(
            0x02);

        writer.WriteString(
            "temp");

        writer.WriteString(
            "Temperature");

        writer.WriteString(
            "degC");

        writer.WriteString(
            "Celsius");

        writer.WriteString(
            "C");
    }
}