using Hase.Core.Domain.Data;

namespace Hase.Core.Tests;

public sealed class ByteArrayValueTests
{
    [Fact]
    public void Constructor_NullArray_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ByteArrayValue(
                (byte[])null!));
    }

    [Fact]
    public void Constructor_EmptyArray_CreatesPresentEmptyValue()
    {
        ByteArrayValue value =
            new(Array.Empty<byte>());

        Assert.Equal(
            0,
            value.Length);

        Assert.Empty(
            value.ToArray());
    }

    [Fact]
    public void Constructor_CopiesSuppliedArray()
    {
        byte[] source =
        {
            0x00,
            0x7F,
            0xFF
        };

        ByteArrayValue value =
            new(source);

        source[1] = 0x01;

        Assert.Equal(
            new byte[]
            {
                0x00,
                0x7F,
                0xFF
            },
            value.ToArray());
    }

    [Fact]
    public void ToArray_ReturnsIndependentCopy()
    {
        ByteArrayValue value =
            new(new byte[]
            {
                0x01,
                0x02
            });

        byte[] copy =
            value.ToArray();

        copy[0] = 0xFF;

        Assert.Equal(
            0x01,
            value[0]);
    }

    [Fact]
    public void AsSpan_PreservesByteOrder()
    {
        ByteArrayValue value =
            new(new byte[]
            {
                0x00,
                0x80,
                0xFF
            });

        Assert.True(
            value.AsSpan().SequenceEqual(
                new byte[]
                {
                    0x00,
                    0x80,
                    0xFF
                }));
    }

    [Fact]
    public void Equality_EqualContent_IsEqual()
    {
        ByteArrayValue left =
            new(new byte[]
            {
                0x01,
                0x02,
                0x03
            });

        ByteArrayValue right =
            new(new byte[]
            {
                0x01,
                0x02,
                0x03
            });

        Assert.Equal(
            left,
            right);

        Assert.True(
            left == right);

        Assert.False(
            left != right);

        Assert.Equal(
            left.GetHashCode(),
            right.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentContent_IsNotEqual()
    {
        ByteArrayValue left =
            new(new byte[]
            {
                0x01,
                0x02
            });

        ByteArrayValue right =
            new(new byte[]
            {
                0x01,
                0x03
            });

        Assert.NotEqual(
            left,
            right);
    }

    [Fact]
    public void Equality_DifferentLength_IsNotEqual()
    {
        ByteArrayValue left =
            new(new byte[]
            {
                0x01
            });

        ByteArrayValue right =
            new(new byte[]
            {
                0x01,
                0x00
            });

        Assert.NotEqual(
            left,
            right);
    }

    [Fact]
    public void Equality_Null_IsNotEqual()
    {
        ByteArrayValue value =
            new(Array.Empty<byte>());

        Assert.False(
            value.Equals(null));

        Assert.False(
            value == null);

        Assert.True(
            value != null);
    }
}
