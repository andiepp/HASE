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

    [Theory]
    [InlineData(0.0, 0x00, 0x00)]
    [InlineData(1.0, 0xE8, 0x03)]
    [InlineData(5.0, 0x88, 0x13)]
    [InlineData(65.535, 0xFF, 0xFF)]
    public void Encode_Volts_ShouldReturnLittleEndianMillivolts(
        double volts,
        byte expectedLow,
        byte expectedHigh)
    {
        ReadOnlyMemory<byte> result = CompactPropertyValueEncoder.Encode(
            CompactPropertyValueEncoding.Unsigned16LittleEndianMillivolts,
            volts);

        Assert.Equal(new byte[] { expectedLow, expectedHigh }, result.ToArray());
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(65.536)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Encode_VoltsOutsideWireRange_ShouldThrow(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CompactPropertyValueEncoder.Encode(
                CompactPropertyValueEncoding.Unsigned16LittleEndianMillivolts,
                value));
    }

    [Fact]
    public void Encode_MillivoltsNonDouble_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            CompactPropertyValueEncoder.Encode(
                CompactPropertyValueEncoding.Unsigned16LittleEndianMillivolts,
                5));
    }
}
