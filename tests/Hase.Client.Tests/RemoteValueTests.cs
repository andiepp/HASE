using Hase.Client;
using Hase.Core.Domain.Data;

namespace Hase.Client.Tests;

public sealed class RemoteValueTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FromBoolean_Value_ShouldSelectOnlyBoolean(
        bool value)
    {
        RemoteValue result =
            RemoteValue.FromBoolean(
                value);

        Assert.Equal(
            RemoteValueKind.Boolean,
            result.Kind);
        Assert.Equal(
            value,
            result.BooleanValue);
        Assert.Null(
            result.StringValue);
        Assert.Null(
            result.NumericValue);
        Assert.Null(
            result.ByteArrayValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("value")]
    public void FromString_Value_ShouldSelectOnlyString(
        string value)
    {
        RemoteValue result =
            RemoteValue.FromString(
                value);

        Assert.Equal(
            RemoteValueKind.String,
            result.Kind);
        Assert.Null(
            result.BooleanValue);
        Assert.Equal(
            value,
            result.StringValue);
        Assert.Null(
            result.NumericValue);
        Assert.Null(
            result.ByteArrayValue);
    }

    [Fact]
    public void FromString_NullValue_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "value",
            () => RemoteValue.FromString(
                null!));
    }

    [Theory]
    [InlineData(-1.5)]
    [InlineData(0.0)]
    [InlineData(1.5)]
    public void FromNumeric_Value_ShouldSelectOnlyNumeric(
        double value)
    {
        RemoteValue result =
            RemoteValue.FromNumeric(
                value);

        Assert.Equal(
            RemoteValueKind.Numeric,
            result.Kind);
        Assert.Null(
            result.BooleanValue);
        Assert.Null(
            result.StringValue);
        Assert.Equal(
            value,
            result.NumericValue);
        Assert.Null(
            result.ByteArrayValue);
    }

    [Fact]
    public void FromByteArray_Value_ShouldSelectOnlyByteArray()
    {
        var value =
            new ByteArrayValue(
                new byte[]
                {
                    0x00,
                    0x7F,
                    0xFF
                });

        RemoteValue result =
            RemoteValue.FromByteArray(
                value);

        Assert.Equal(
            RemoteValueKind.ByteArray,
            result.Kind);
        Assert.Null(
            result.BooleanValue);
        Assert.Null(
            result.StringValue);
        Assert.Null(
            result.NumericValue);
        Assert.Same(
            value,
            result.ByteArrayValue);
    }

    [Fact]
    public void FromByteArray_NullValue_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "value",
            () =>
                RemoteValue.FromByteArray(
                    null!));
    }

    [Fact]
    public void Equality_SameSelectedValue_ShouldBeEqual()
    {
        Assert.Equal(
            RemoteValue.FromNumeric(
                12.5),
            RemoteValue.FromNumeric(
                12.5));
    }

    [Fact]
    public void Equality_DifferentKinds_ShouldNotBeEqual()
    {
        Assert.NotEqual(
            RemoteValue.FromBoolean(
                true),
            RemoteValue.FromString(
                "true"));
    }
}
