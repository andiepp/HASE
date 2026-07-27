using Hase.Client;

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
