namespace Hase.CompactProtocol.Tests;

public sealed class CompactPropertyValueEncoderTests
{
    [Fact]
    public void Encode_BooleanFalse_ShouldReturnZeroByte()
    {
        ReadOnlyMemory<byte> result =
            CompactPropertyValueEncoder.Encode(
                CompactPropertyValueEncoding.Boolean,
                false);

        Assert.Equal(
            new byte[]
            {
                0x00
            },
            result.ToArray());
    }

    [Fact]
    public void Encode_BooleanTrue_ShouldReturnOneByte()
    {
        ReadOnlyMemory<byte> result =
            CompactPropertyValueEncoder.Encode(
                CompactPropertyValueEncoding.Boolean,
                true);

        Assert.Equal(
            new byte[]
            {
                0x01
            },
            result.ToArray());
    }

    [Theory]
    [InlineData(
        0)]
    [InlineData(
        1)]
    [InlineData(
        "true")]
    public void Encode_BooleanNonBooleanValue_ShouldThrow(
        object value)
    {
        void Act()
        {
            _ = CompactPropertyValueEncoder.Encode(
                CompactPropertyValueEncoding.Boolean,
                value);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Encode_NullValue_ShouldThrow()
    {
        void Act()
        {
            _ = CompactPropertyValueEncoder.Encode(
                CompactPropertyValueEncoding.Boolean,
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Encode_UndefinedEncoding_ShouldThrow()
    {
        void Act()
        {
            _ = CompactPropertyValueEncoder.Encode(
                (CompactPropertyValueEncoding)0xFF,
                true);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }
}