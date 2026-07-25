using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class PropertyDescriptorMapperTests
{
    [Fact]
    public void Constructor_NullDataDescriptorMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "dataDescriptorMapper",
            () =>
                new PropertyDescriptorMapper(
                    null!));
    }

    [Fact]
    public void Map_NullDescriptor_ShouldThrow()
    {
        var mapper =
            new PropertyDescriptorMapper(
                new TestDataDescriptorMapper(
                    new GrpcV1.DataDescriptor()));

        Assert.Throws<ArgumentNullException>(
            "descriptor",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_RequiredMembers_ShouldPreserveIdentityPathAndMappedData()
    {
        var sourceData =
            new BooleanDataDescriptor();
        var mappedData =
            new GrpcV1.DataDescriptor
            {
                BooleanDescriptor =
                    new GrpcV1.BooleanDataDescriptor()
            };

        var dataMapper =
            new TestDataDescriptorMapper(
                mappedData);

        var mapper =
            new PropertyDescriptorMapper(
                dataMapper);

        GrpcV1.PropertyDescriptor result =
            mapper.Map(
                new PropertyDescriptor(
                    new PropertyId(
                        "property-1"),
                    new DescriptorPath(
                        "Environment",
                        "Temperature"),
                    "Temperature",
                    sourceData));

        Assert.Equal(
            "property-1",
            result.PropertyId);
        Assert.Equal(
            new[]
            {
                "Environment",
                "Temperature"
            },
            result.PathSegments.ToArray());
        Assert.Equal(
            "Temperature",
            result.DisplayName);
        Assert.False(
            result.HasDescription);
        Assert.Same(
            mappedData,
            result.Data);
        Assert.Same(
            sourceData,
            dataMapper.Input);
    }

    [Fact]
    public void Map_Description_ShouldPreserveOptionalValue()
    {
        var mapper =
            new PropertyDescriptorMapper(
                new TestDataDescriptorMapper(
                    new GrpcV1.DataDescriptor()));

        GrpcV1.PropertyDescriptor result =
            mapper.Map(
                CreateDescriptor() with
                {
                    Description =
                        "Current measured temperature."
                });

        Assert.True(
            result.HasDescription);
        Assert.Equal(
            "Current measured temperature.",
            result.Description);
    }

    [Theory]
    [InlineData(PropertyAccessMode.None, 0)]
    [InlineData(PropertyAccessMode.Read, 1)]
    [InlineData(PropertyAccessMode.Write, 2)]
    [InlineData(PropertyAccessMode.ReadWrite, 3)]
    public void Map_AccessMode_ShouldUseStableRemoteValue(
        PropertyAccessMode source,
        int expectedRemoteValue)
    {
        var mapper =
            new PropertyDescriptorMapper(
                new TestDataDescriptorMapper(
                    new GrpcV1.DataDescriptor()));

        GrpcV1.PropertyDescriptor result =
            mapper.Map(
                CreateDescriptor() with
                {
                    AccessMode =
                        source
                });

        Assert.Equal(
            expectedRemoteValue,
            (int)result.AccessMode);
    }

    [Fact]
    public void Map_UnknownAccessMode_ShouldThrow()
    {
        const PropertyAccessMode unknownAccessMode =
            (PropertyAccessMode)4;

        var mapper =
            new PropertyDescriptorMapper(
                new TestDataDescriptorMapper(
                    new GrpcV1.DataDescriptor()));

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                "accessMode",
                () =>
                    mapper.Map(
                        CreateDescriptor() with
                        {
                            AccessMode =
                                unknownAccessMode
                        }));

        Assert.Equal(
            unknownAccessMode,
            exception.ActualValue);
    }

    [Fact]
    public void Map_DataMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new PropertyDescriptorMapper(
                new TestDataDescriptorMapper(
                    null!));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreateDescriptor()));

        Assert.Equal(
            "The data descriptor mapper returned null.",
            exception.Message);
    }

    private static PropertyDescriptor CreateDescriptor()
    {
        return new PropertyDescriptor(
            new PropertyId(
                "property-1"),
            new DescriptorPath(
                "Property"),
            "Property",
            new BooleanDataDescriptor());
    }

    private sealed class TestDataDescriptorMapper
        : IDataDescriptorMapper
    {
        private readonly GrpcV1.DataDescriptor result;

        public TestDataDescriptorMapper(
            GrpcV1.DataDescriptor result)
        {
            this.result =
                result;
        }

        public DataDescriptor? Input
        {
            get;
            private set;
        }

        public GrpcV1.DataDescriptor Map(
            DataDescriptor descriptor)
        {
            Input =
                descriptor;

            return result;
        }
    }
}
