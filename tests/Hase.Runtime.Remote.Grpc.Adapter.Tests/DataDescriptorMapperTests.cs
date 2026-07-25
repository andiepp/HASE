using Hase.Core.Domain.Data;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class DataDescriptorMapperTests
{
    [Fact]
    public void Constructor_NullNumericMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "numericDataDescriptorMapper",
            () =>
                new DataDescriptorMapper(
                    null!));
    }

    [Fact]
    public void Map_NullDescriptor_ShouldThrow()
    {
        var mapper =
            new DataDescriptorMapper(
                new TestNumericMapper(
                    new GrpcV1.NumericDataDescriptor()));

        Assert.Throws<ArgumentNullException>(
            "descriptor",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_BooleanDescriptor_ShouldSelectBooleanVariant()
    {
        var numericMapper =
            new TestNumericMapper(
                new GrpcV1.NumericDataDescriptor());

        var mapper =
            new DataDescriptorMapper(
                numericMapper);

        GrpcV1.DataDescriptor result =
            mapper.Map(
                new BooleanDataDescriptor());

        Assert.Equal(
            GrpcV1.DataDescriptor.KindOneofCase.BooleanDescriptor,
            result.KindCase);
        Assert.NotNull(
            result.BooleanDescriptor);
        Assert.Null(
            numericMapper.Input);
    }

    [Fact]
    public void Map_StringDescriptor_ShouldSelectStringVariant()
    {
        var numericMapper =
            new TestNumericMapper(
                new GrpcV1.NumericDataDescriptor());

        var mapper =
            new DataDescriptorMapper(
                numericMapper);

        GrpcV1.DataDescriptor result =
            mapper.Map(
                new StringDataDescriptor());

        Assert.Equal(
            GrpcV1.DataDescriptor.KindOneofCase.StringDescriptor,
            result.KindCase);
        Assert.NotNull(
            result.StringDescriptor);
        Assert.Null(
            numericMapper.Input);
    }

    [Fact]
    public void Map_NumericDescriptor_ShouldDelegateAndSelectNumericVariant()
    {
        NumericDataDescriptor source =
            CreateNumericDescriptor();
        var mapped =
            new GrpcV1.NumericDataDescriptor();

        var numericMapper =
            new TestNumericMapper(
                mapped);

        var mapper =
            new DataDescriptorMapper(
                numericMapper);

        GrpcV1.DataDescriptor result =
            mapper.Map(
                source);

        Assert.Equal(
            GrpcV1.DataDescriptor.KindOneofCase.Numeric,
            result.KindCase);
        Assert.Same(
            mapped,
            result.Numeric);
        Assert.Same(
            source,
            numericMapper.Input);
    }

    [Fact]
    public void Map_NumericMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new DataDescriptorMapper(
                new TestNumericMapper(
                    null!));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreateNumericDescriptor()));

        Assert.Equal(
            "The numeric data descriptor mapper returned null.",
            exception.Message);
    }

    [Fact]
    public void Map_UnsupportedDescriptor_ShouldThrow()
    {
        var descriptor =
            new UnsupportedDataDescriptor();

        var mapper =
            new DataDescriptorMapper(
                new TestNumericMapper(
                    new GrpcV1.NumericDataDescriptor()));

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                "descriptor",
                () =>
                    mapper.Map(
                        descriptor));

        Assert.Same(
            descriptor,
            exception.ActualValue);
    }

    private static NumericDataDescriptor CreateNumericDescriptor()
    {
        Quantity quantity =
            Quantities.Temperature;

        return new NumericDataDescriptor(
            quantity,
            Units.Celsius);
    }

    private sealed record UnsupportedDataDescriptor
        : DataDescriptor;

    private sealed class TestNumericMapper
        : INumericDataDescriptorMapper
    {
        private readonly GrpcV1.NumericDataDescriptor result;

        public TestNumericMapper(
            GrpcV1.NumericDataDescriptor result)
        {
            this.result =
                result;
        }

        public NumericDataDescriptor? Input
        {
            get;
            private set;
        }

        public GrpcV1.NumericDataDescriptor Map(
            NumericDataDescriptor descriptor)
        {
            Input =
                descriptor;

            return result;
        }
    }
}
