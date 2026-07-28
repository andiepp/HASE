using Hase.Core.Domain.Data;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RemoteValueMapperTests
{
    [Fact]
    public void Map_NullValue_ShouldThrow()
    {
        var mapper =
            new RemoteValueMapper();

        Assert.Throws<ArgumentNullException>(
            "value",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_BooleanValue_ShouldSelectBooleanVariant()
    {
        var mapper =
            new RemoteValueMapper();

        GrpcV1.RemoteValue result =
            mapper.Map(
                true);

        Assert.Equal(
            GrpcV1.RemoteValue.KindOneofCase.BooleanValue,
            result.KindCase);
        Assert.True(
            result.BooleanValue);
    }

    [Fact]
    public void Map_StringValue_ShouldSelectStringVariant()
    {
        var mapper =
            new RemoteValueMapper();

        GrpcV1.RemoteValue result =
            mapper.Map(
                "Ready");

        Assert.Equal(
            GrpcV1.RemoteValue.KindOneofCase.StringValue,
            result.KindCase);
        Assert.Equal(
            "Ready",
            result.StringValue);
    }

    [Fact]
    public void Map_ByteArrayValue_ShouldSelectByteArrayVariant()
    {
        var source =
            new ByteArrayValue(
                new byte[]
                {
                    0x00,
                    0x7F,
                    0xFF
                });

        var mapper =
            new RemoteValueMapper();

        GrpcV1.RemoteValue result =
            mapper.Map(
                source);

        Assert.Equal(
            GrpcV1.RemoteValue.KindOneofCase.ByteArrayValue,
            result.KindCase);
        Assert.Equal(
            source.ToArray(),
            result.ByteArrayValue.ToByteArray());
    }

    [Theory]
    [MemberData(nameof(NumericValues))]
    public void Map_NumericValue_ShouldNormalizeToDouble(
        object source,
        double expected)
    {
        var mapper =
            new RemoteValueMapper();

        GrpcV1.RemoteValue result =
            mapper.Map(
                source);

        Assert.Equal(
            GrpcV1.RemoteValue.KindOneofCase.NumericValue,
            result.KindCase);
        Assert.Equal(
            expected,
            result.NumericValue);
    }

    [Fact]
    public void Map_UnsupportedValue_ShouldThrow()
    {
        var value =
            new object();

        var mapper =
            new RemoteValueMapper();

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                "value",
                () =>
                    mapper.Map(
                        value));

        Assert.Same(
            value,
            exception.ActualValue);
    }

    [Fact]
    public void MapToClr_NullValue_ShouldThrow()
    {
        var mapper =
            new RemoteValueMapper();

        Assert.Throws<ArgumentNullException>(
            "value",
            () =>
                mapper.MapToClr(
                    null!));
    }

    [Fact]
    public void MapToClr_AbsentVariant_ShouldReturnNull()
    {
        var mapper =
            new RemoteValueMapper();

        object? result =
            mapper.MapToClr(
                new GrpcV1.RemoteValue());

        Assert.Null(
            result);
    }

    [Fact]
    public void MapToClr_DefinedVariants_ShouldReturnNormalizedValues()
    {
        var mapper =
            new RemoteValueMapper();

        Assert.Equal(
            true,
            mapper.MapToClr(
                new GrpcV1.RemoteValue
                {
                    BooleanValue =
                        true
                }));
        Assert.Equal(
            "Ready",
            mapper.MapToClr(
                new GrpcV1.RemoteValue
                {
                    StringValue =
                        "Ready"
                }));
        Assert.Equal(
            23.75,
            mapper.MapToClr(
                new GrpcV1.RemoteValue
                {
                    NumericValue =
                        23.75
                }));
    }

    [Fact]
    public void MapToClr_ByteArrayVariant_ShouldReturnByteArrayValue()
    {
        var source =
            new GrpcV1.RemoteValue
            {
                ByteArrayValue =
                    Google.Protobuf.ByteString.CopyFrom(
                        new byte[]
                        {
                            0x00,
                            0x7F,
                            0xFF
                        })
            };

        var mapper =
            new RemoteValueMapper();

        var result =
            Assert.IsType<ByteArrayValue>(
                mapper.MapToClr(
                    source));

        Assert.Equal(
            source.ByteArrayValue.ToByteArray(),
            result.ToArray());
    }

    public static TheoryData<object, double> NumericValues
    {
        get;
    } =
        new()
        {
            {
                (byte)12,
                12.0
            },
            {
                (sbyte)-12,
                -12.0
            },
            {
                (short)-1234,
                -1234.0
            },
            {
                (ushort)1234,
                1234.0
            },
            {
                -123456,
                -123456.0
            },
            {
                123456U,
                123456.0
            },
            {
                -123456789L,
                -123456789.0
            },
            {
                123456789UL,
                123456789.0
            },
            {
                23.5F,
                23.5
            },
            {
                23.75,
                23.75
            },
            {
                23.125M,
                23.125
            }
        };
}
