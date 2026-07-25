using Hase.Core.Domain.Data;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class NumericDataDescriptorMapperTests
{
    [Fact]
    public void Constructor_NullChildMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "quantityMapper",
            () =>
                new NumericDataDescriptorMapper(
                    null!,
                    new TestUnitMapper(
                        new GrpcV1.Unit())));

        Assert.Throws<ArgumentNullException>(
            "unitMapper",
            () =>
                new NumericDataDescriptorMapper(
                    new TestQuantityMapper(
                        new GrpcV1.Quantity()),
                    null!));
    }

    [Fact]
    public void Map_NullDescriptor_ShouldThrow()
    {
        NumericDataDescriptorMapper mapper =
            CreateMapper();

        Assert.Throws<ArgumentNullException>(
            "descriptor",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_RequiredMembers_ShouldDelegateAndLeaveOptionalsAbsent()
    {
        Quantity quantity =
            Quantities.Temperature;
        Unit unit =
            Units.Celsius;
        var mappedQuantity =
            new GrpcV1.Quantity
            {
                Id =
                    "mapped-temperature"
            };
        var mappedUnit =
            new GrpcV1.Unit
            {
                Id =
                    "mapped-celsius"
            };

        var quantityMapper =
            new TestQuantityMapper(
                mappedQuantity);
        var unitMapper =
            new TestUnitMapper(
                mappedUnit);

        var mapper =
            new NumericDataDescriptorMapper(
                quantityMapper,
                unitMapper);

        GrpcV1.NumericDataDescriptor result =
            mapper.Map(
                new NumericDataDescriptor(
                    quantity,
                    unit));

        Assert.Same(
            mappedQuantity,
            result.Quantity);
        Assert.Same(
            mappedUnit,
            result.NativeUnit);
        Assert.Same(
            quantity,
            quantityMapper.Input);
        Assert.Same(
            unit,
            unitMapper.Input);
        Assert.Null(
            result.Range);
        Assert.Null(
            result.Resolution);
    }

    [Fact]
    public void Map_OptionalRangeAndResolution_ShouldPreserveValues()
    {
        NumericDataDescriptorMapper mapper =
            CreateMapper();

        GrpcV1.NumericDataDescriptor result =
            mapper.Map(
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius,
                    new ValueRange(
                        -40.0,
                        85.0),
                    new Resolution(
                        0.01)));

        Assert.NotNull(
            result.Range);
        Assert.Equal(
            -40.0,
            result.Range.Minimum);
        Assert.Equal(
            85.0,
            result.Range.Maximum);
        Assert.NotNull(
            result.Resolution);
        Assert.Equal(
            0.01,
            result.Resolution.Value);
    }

    [Fact]
    public void Map_QuantityMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new NumericDataDescriptorMapper(
                new TestQuantityMapper(
                    null!),
                new TestUnitMapper(
                    new GrpcV1.Unit()));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreateDescriptor()));

        Assert.Equal(
            "The quantity mapper returned null.",
            exception.Message);
    }

    [Fact]
    public void Map_UnitMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new NumericDataDescriptorMapper(
                new TestQuantityMapper(
                    new GrpcV1.Quantity()),
                new TestUnitMapper(
                    null!));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreateDescriptor()));

        Assert.Equal(
            "The unit mapper returned null.",
            exception.Message);
    }

    private static NumericDataDescriptorMapper CreateMapper()
    {
        return new NumericDataDescriptorMapper(
            new TestQuantityMapper(
                new GrpcV1.Quantity()),
            new TestUnitMapper(
                new GrpcV1.Unit()));
    }

    private static NumericDataDescriptor CreateDescriptor()
    {
        return new NumericDataDescriptor(
            Quantities.Temperature,
            Units.Celsius);
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

    private sealed class TestUnitMapper
        : IUnitMapper
    {
        private readonly GrpcV1.Unit result;

        public TestUnitMapper(
            GrpcV1.Unit result)
        {
            this.result =
                result;
        }

        public Unit? Input
        {
            get;
            private set;
        }

        public GrpcV1.Unit Map(
            Unit unit)
        {
            Input =
                unit;

            return result;
        }
    }
}
