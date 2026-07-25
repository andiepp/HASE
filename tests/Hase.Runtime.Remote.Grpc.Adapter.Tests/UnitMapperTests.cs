using Hase.Core.Domain.Data;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class UnitMapperTests
{
    [Fact]
    public void Constructor_NullQuantityMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "quantityMapper",
            () =>
                new UnitMapper(
                    null!));
    }

    [Fact]
    public void Map_NullUnit_ShouldThrow()
    {
        var mapper =
            new UnitMapper(
                new TestQuantityMapper(
                    new GrpcV1.Quantity()));

        Assert.Throws<ArgumentNullException>(
            "unit",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_Unit_ShouldPreserveMembersAndDelegateQuantity()
    {
        var quantity =
            new Quantity(
                "temperature",
                "Temperature");
        var unit =
            new Unit(
                "celsius",
                "Degree Celsius",
                "°C",
                quantity);
        var mappedQuantity =
            new GrpcV1.Quantity
            {
                Id =
                    "mapped-temperature"
            };

        var quantityMapper =
            new TestQuantityMapper(
                mappedQuantity);

        var mapper =
            new UnitMapper(
                quantityMapper);

        GrpcV1.Unit result =
            mapper.Map(
                unit);

        Assert.Equal(
            "celsius",
            result.Id);
        Assert.Equal(
            "Degree Celsius",
            result.DisplayName);
        Assert.Equal(
            "°C",
            result.Symbol);
        Assert.Same(
            mappedQuantity,
            result.Quantity);
        Assert.Same(
            quantity,
            quantityMapper.Input);
    }

    [Fact]
    public void Map_QuantityMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new UnitMapper(
                new TestQuantityMapper(
                    null!));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        Units.Celsius));

        Assert.Equal(
            "The quantity mapper returned null.",
            exception.Message);
    }

    private sealed class TestQuantityMapper
        : IQuantityMapper
    {
        private readonly GrpcV1.Quantity result;

        public TestQuantityMapper(
            GrpcV1.Quantity result)
        {
            this.result =
                result;
        }

        public Quantity? Input
        {
            get;
            private set;
        }

        public GrpcV1.Quantity Map(
            Quantity quantity)
        {
            Input =
                quantity;

            return result;
        }
    }
}
