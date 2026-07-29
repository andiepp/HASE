using Hase.Client.Wpf.Services;
using Hase.Core.Domain.Data;

namespace Hase.Client.Wpf.Tests;

public sealed class PropertyInputRemoteValueMapperTests
{
    [Fact]
    public void Map_Boolean_ShouldCreateBooleanRemoteValue()
    {
        RemoteValue result =
            PropertyInputRemoteValueMapper.Map(
                true);

        Assert.Equal(
            RemoteValueKind.Boolean,
            result.Kind);
        Assert.True(
            result.BooleanValue);
    }

    [Fact]
    public void Map_Numeric_ShouldCreateNumericRemoteValue()
    {
        RemoteValue result =
            PropertyInputRemoteValueMapper.Map(
                23.5);

        Assert.Equal(
            RemoteValueKind.Numeric,
            result.Kind);
        Assert.Equal(
            23.5,
            result.NumericValue);
    }

    [Fact]
    public void Map_String_ShouldPreserveExactText()
    {
        RemoteValue result =
            PropertyInputRemoteValueMapper.Map(
                "  exact text  ");

        Assert.Equal(
            RemoteValueKind.String,
            result.Kind);
        Assert.Equal(
            "  exact text  ",
            result.StringValue);
    }

    [Fact]
    public void Map_ByteArray_ShouldPreserveExactBytes()
    {
        RemoteValue result =
            PropertyInputRemoteValueMapper.Map(
                new ByteArrayValue(
                    new byte[]
                    {
                        0x00,
                        0x53,
                        0xFF
                    }));

        Assert.Equal(
            RemoteValueKind.ByteArray,
            result.Kind);
        Assert.Equal(
            new byte[]
            {
                0x00,
                0x53,
                0xFF
            },
            result.ByteArrayValue!.ToArray());
    }

    [Fact]
    public void Map_UnsupportedValue_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () =>
                PropertyInputRemoteValueMapper.Map(
                    17));
    }
}
