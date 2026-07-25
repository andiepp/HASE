using Hase.Core.Domain.Properties;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostPropertyOperationResultMapperTests
{
    [Fact]
    public void Constructor_NullStatusMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "statusMapper",
            () =>
                new RuntimeHostPropertyOperationResultMapper(
                    null!,
                    CreatePropertyValueMapper()));
    }

    [Fact]
    public void Constructor_NullPropertyValueMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "propertyValueMapper",
            () =>
                new RuntimeHostPropertyOperationResultMapper(
                    CreateStatusMapper(),
                    null!));
    }

    [Fact]
    public void Map_NullResult_ShouldThrow()
    {
        var mapper =
            CreateMapper();

        Assert.Throws<ArgumentNullException>(
            "result",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_Success_ShouldMapStatusAndConfirmedValue()
    {
        var confirmedValue =
            new PropertyValue(
                23.75,
                DateTimeOffset.UnixEpoch);
        var mappedValue =
            new GrpcV1.PropertyValue
            {
                Quality =
                    GrpcV1.PropertyQuality.Good
            };
        var statusMapper =
            CreateStatusMapper();
        var propertyValueMapper =
            new TestPropertyValueMapper(
                mappedValue);
        var mapper =
            new RuntimeHostPropertyOperationResultMapper(
                statusMapper,
                propertyValueMapper);

        GrpcV1.PropertyOperationResult result =
            mapper.Map(
                Northbound.RuntimeHostPropertyOperationResult.Successful(
                    confirmedValue));

        Assert.Equal(
            Northbound.RuntimeHostPropertyOperationStatus.Success,
            statusMapper.Input);
        Assert.Equal(
            GrpcV1.PropertyOperationStatus.Success,
            result.Status);
        Assert.Same(
            confirmedValue,
            propertyValueMapper.Input);
        Assert.Same(
            mappedValue,
            result.ConfirmedValue);
        Assert.False(
            result.HasDiagnostic);
    }

    [Fact]
    public void Map_Failure_ShouldMapStatusDiagnosticAndValueAbsence()
    {
        var statusMapper =
            new TestStatusMapper(
                GrpcV1.PropertyOperationStatus.EndpointRejected);
        var propertyValueMapper =
            CreatePropertyValueMapper();
        var mapper =
            new RuntimeHostPropertyOperationResultMapper(
                statusMapper,
                propertyValueMapper);

        GrpcV1.PropertyOperationResult result =
            mapper.Map(
                Northbound.RuntimeHostPropertyOperationResult.Failed(
                    Northbound.RuntimeHostPropertyOperationStatus.EndpointRejected,
                    "Endpoint rejected the value."));

        Assert.Equal(
            Northbound.RuntimeHostPropertyOperationStatus.EndpointRejected,
            statusMapper.Input);
        Assert.Equal(
            GrpcV1.PropertyOperationStatus.EndpointRejected,
            result.Status);
        Assert.Null(
            propertyValueMapper.Input);
        Assert.Null(
            result.ConfirmedValue);
        Assert.True(
            result.HasDiagnostic);
        Assert.Equal(
            "Endpoint rejected the value.",
            result.Diagnostic);
    }

    [Fact]
    public void Map_PropertyValueMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new RuntimeHostPropertyOperationResultMapper(
                CreateStatusMapper(),
                new TestPropertyValueMapper(
                    null!));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        Northbound.RuntimeHostPropertyOperationResult.Successful(
                            new PropertyValue(
                                true,
                                DateTimeOffset.UnixEpoch))));

        Assert.Equal(
            "The Property value mapper returned null.",
            exception.Message);
    }

    private static RuntimeHostPropertyOperationResultMapper CreateMapper()
    {
        return new RuntimeHostPropertyOperationResultMapper(
            CreateStatusMapper(),
            CreatePropertyValueMapper());
    }

    private static TestStatusMapper CreateStatusMapper()
    {
        return new TestStatusMapper(
            GrpcV1.PropertyOperationStatus.Success);
    }

    private static TestPropertyValueMapper CreatePropertyValueMapper()
    {
        return new TestPropertyValueMapper(
            new GrpcV1.PropertyValue());
    }

    private sealed class TestStatusMapper
        : IRuntimeHostPropertyOperationStatusMapper
    {
        private readonly GrpcV1.PropertyOperationStatus result;

        public TestStatusMapper(
            GrpcV1.PropertyOperationStatus result)
        {
            this.result =
                result;
        }

        public Northbound.RuntimeHostPropertyOperationStatus? Input
        {
            get;
            private set;
        }

        public GrpcV1.PropertyOperationStatus Map(
            Northbound.RuntimeHostPropertyOperationStatus status)
        {
            Input =
                status;

            return result;
        }
    }

    private sealed class TestPropertyValueMapper
        : IPropertyValueMapper
    {
        private readonly GrpcV1.PropertyValue result;

        public TestPropertyValueMapper(
            GrpcV1.PropertyValue result)
        {
            this.result =
                result;
        }

        public PropertyValue? Input
        {
            get;
            private set;
        }

        public GrpcV1.PropertyValue Map(
            PropertyValue source)
        {
            Input =
                source;

            return result;
        }
    }
}
