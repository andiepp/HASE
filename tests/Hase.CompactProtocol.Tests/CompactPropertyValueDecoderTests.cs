namespace Hase.CompactProtocol.Tests;

public sealed class CompactPropertyValueDecoderTests
{
    [Fact]
    public void Decode_BooleanFalse_ShouldReturnFalse()
    {
        object result =
            CompactPropertyValueDecoder.Decode(
                CompactPropertyValueEncoding.Boolean,
                value:
                [
                    0x00
                ]);

        bool value =
            Assert.IsType<bool>(
                result);

        Assert.False(
            value);
    }

    [Fact]
    public void Decode_BooleanTrue_ShouldReturnTrue()
    {
        object result =
            CompactPropertyValueDecoder.Decode(
                CompactPropertyValueEncoding.Boolean,
                value:
                [
                    0x01
                ]);

        bool value =
            Assert.IsType<bool>(
                result);

        Assert.True(
            value);
    }

    [Fact]
    public void Decode_BooleanEmptyValue_ShouldThrow()
    {
        void Act()
        {
            _ = CompactPropertyValueDecoder.Decode(
                CompactPropertyValueEncoding.Boolean,
                value: []);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Decode_BooleanMultipleBytes_ShouldThrow()
    {
        void Act()
        {
            _ = CompactPropertyValueDecoder.Decode(
                CompactPropertyValueEncoding.Boolean,
                value:
                [
                    0x00,
                    0x01
                ]);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Theory]
    [InlineData(
        0x02)]
    [InlineData(
        0x7F)]
    [InlineData(
        0xFF)]
    public void Decode_BooleanUnknownByte_ShouldThrow(
        byte value)
    {
        void Act()
        {
            _ = CompactPropertyValueDecoder.Decode(
                CompactPropertyValueEncoding.Boolean,
                new byte[]
                {
                    value
                });
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Decode_UndefinedEncoding_ShouldThrow()
    {
        void Act()
        {
            _ = CompactPropertyValueDecoder.Decode(
                (CompactPropertyValueEncoding)0xFF,
                value:
                [
                    0x00
                ]);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Theory]
    [InlineData(0x00, 0x00, 0.0)]
    [InlineData(0xE8, 0x03, 1.0)]
    [InlineData(0x88, 0x13, 5.0)]
    [InlineData(0xFF, 0xFF, 65.535)]
    public void Decode_Millivolts_ShouldReturnVolts(
        byte lowByte,
        byte highByte,
        double expected)
    {
        object result = CompactPropertyValueDecoder.Decode(
            CompactPropertyValueEncoding.Unsigned16LittleEndianMillivolts,
            [lowByte, highByte]);

        Assert.Equal(expected, Assert.IsType<double>(result), precision: 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Decode_MillivoltsWrongLength_ShouldThrow(int length)
    {
        Assert.Throws<InvalidDataException>(() =>
            CompactPropertyValueDecoder.Decode(
                CompactPropertyValueEncoding.Unsigned16LittleEndianMillivolts,
                new byte[length]));
    }

    [Theory]
    [InlineData(0x00, 0x00, 0.0)]
    [InlineData(0x01, 0x00, 1.0)]
    [InlineData(0xE8, 0x03, 1000.0)]
    [InlineData(0xFF, 0xFF, 65535.0)]
    public void Decode_Unsigned16_ShouldReturnRawValue(
        byte lowByte,
        byte highByte,
        double expected)
    {
        object result = CompactPropertyValueDecoder.Decode(
            CompactPropertyValueEncoding.Unsigned16LittleEndian,
            [lowByte, highByte]);

        Assert.Equal(expected, Assert.IsType<double>(result), precision: 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Decode_Unsigned16WrongLength_ShouldThrow(int length)
    {
        Assert.Throws<InvalidDataException>(() =>
            CompactPropertyValueDecoder.Decode(
                CompactPropertyValueEncoding.Unsigned16LittleEndian,
                new byte[length]));
    }
}
