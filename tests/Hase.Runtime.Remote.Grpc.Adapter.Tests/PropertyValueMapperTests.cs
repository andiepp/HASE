using Google.Protobuf.WellKnownTypes;
using CoreProperties = global::Hase.Core.Domain.Properties;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class PropertyValueMapperTests
{
    [Fact]
    public void Constructor_NullValueMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "valueMapper",
            () =>
                new PropertyValueMapper(
                    null!));
    }

    [Fact]
    public void Map_NullSource_ShouldThrow()
    {
        var mapper =
            new PropertyValueMapper(
                new TestRemoteValueMapper(
                    new GrpcV1.RemoteValue()));

        Assert.Throws<ArgumentNullException>(
            "source",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_DefinedValue_ShouldPreserveTimestampAndDelegateValue()
    {
        var timestampUtc =
            new DateTimeOffset(
                2026,
                7,
                25,
                13,
                45,
                30,
                TimeSpan.Zero);
        var mappedValue =
            new GrpcV1.RemoteValue
            {
                NumericValue =
                    23.75
            };
        var valueMapper =
            new TestRemoteValueMapper(
                mappedValue);
        var mapper =
            new PropertyValueMapper(
                valueMapper);
        var source =
            new CoreProperties.PropertyValue(
                23.75,
                timestampUtc,
                CoreProperties.PropertyQuality.Good);

        GrpcV1.PropertyValue result =
            mapper.Map(
                source);

        Assert.Same(
            source.Value,
            valueMapper.Input);
        Assert.Same(
            mappedValue,
            result.Value);
        Assert.Equal(
            Timestamp.FromDateTimeOffset(
                timestampUtc),
            result.TimestampUtc);
        Assert.Equal(
            GrpcV1.PropertyQuality.Good,
            result.Quality);
    }

    [Fact]
    public void Map_NullValue_ShouldPreserveAbsence()
    {
        var valueMapper =
            new TestRemoteValueMapper(
                new GrpcV1.RemoteValue());
        var mapper =
            new PropertyValueMapper(
                valueMapper);

        GrpcV1.PropertyValue result =
            mapper.Map(
                new CoreProperties.PropertyValue(
                    null,
                    DateTimeOffset.UnixEpoch,
                    CoreProperties.PropertyQuality.Uncertain));

        Assert.Null(
            valueMapper.Input);
        Assert.Null(
            result.Value);
        Assert.Equal(
            Timestamp.FromDateTimeOffset(
                DateTimeOffset.UnixEpoch),
            result.TimestampUtc);
        Assert.Equal(
            GrpcV1.PropertyQuality.Uncertain,
            result.Quality);
    }

    [Theory]
    [InlineData(CoreProperties.PropertyQuality.Good, 1)]
    [InlineData(CoreProperties.PropertyQuality.Uncertain, 2)]
    [InlineData(CoreProperties.PropertyQuality.Bad, 3)]
    public void Map_Quality_ShouldUseStableRemoteValue(
        CoreProperties.PropertyQuality sourceQuality,
        int expectedRemoteValue)
    {
        var mapper =
            new PropertyValueMapper(
                new TestRemoteValueMapper(
                    new GrpcV1.RemoteValue()));

        GrpcV1.PropertyValue result =
            mapper.Map(
                new CoreProperties.PropertyValue(
                    null,
                    DateTimeOffset.UnixEpoch,
                    sourceQuality));

        Assert.Equal(
            expectedRemoteValue,
            (int)result.Quality);
    }

    [Fact]
    public void Map_UnknownQuality_ShouldThrow()
    {
        const CoreProperties.PropertyQuality unknownQuality =
            (CoreProperties.PropertyQuality)99;
        var mapper =
            new PropertyValueMapper(
                new TestRemoteValueMapper(
                    new GrpcV1.RemoteValue()));

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                "quality",
                () =>
                    mapper.Map(
                        new CoreProperties.PropertyValue(
                            null,
                            DateTimeOffset.UnixEpoch,
                            unknownQuality)));

        Assert.Equal(
            unknownQuality,
            exception.ActualValue);
    }

    [Fact]
    public void Map_ValueMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new PropertyValueMapper(
                new TestRemoteValueMapper(
                    null!));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        new CoreProperties.PropertyValue(
                            true,
                            DateTimeOffset.UnixEpoch)));

        Assert.Equal(
            "The remote value mapper returned null.",
            exception.Message);
    }

    private sealed class TestRemoteValueMapper
        : IRemoteValueMapper
    {
        private readonly GrpcV1.RemoteValue result;

        public TestRemoteValueMapper(
            GrpcV1.RemoteValue result)
        {
            this.result =
                result;
        }

        public object? Input
        {
            get;
            private set;
        }

        public GrpcV1.RemoteValue Map(
            object value)
        {
            Input =
                value;

            return result;
        }
    }
}
