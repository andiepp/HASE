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
}