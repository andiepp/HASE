using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostCommandOperationResultMapperTests
{
    [Fact]
    public void Constructor_NullStatusMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "statusMapper",
            () =>
                new RuntimeHostCommandOperationResultMapper(
                    null!,
                    CreateRemoteValueMapper()));
    }

    [Fact]
    public void Constructor_NullRemoteValueMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "remoteValueMapper",
            () =>
                new RuntimeHostCommandOperationResultMapper(
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
    public void Map_SuccessWithReturnValue_ShouldMapStatusAndValue()
    {
        var mappedValue =
            new GrpcV1.RemoteValue
            {
                BooleanValue =
                    true
            };
        var statusMapper =
            CreateStatusMapper();
        var remoteValueMapper =
            new TestRemoteValueMapper(
                mappedValue);
        var mapper =
            new RuntimeHostCommandOperationResultMapper(
                statusMapper,
                remoteValueMapper);

        GrpcV1.CommandOperationResult result =
            mapper.Map(
                Northbound.RuntimeHostCommandOperationResult.Successful(
                    true));

        Assert.Equal(
            Northbound.RuntimeHostCommandOperationStatus.Success,
            statusMapper.Input);
        Assert.Equal(
            GrpcV1.CommandOperationStatus.Success,
            result.Status);
        Assert.Equal(
            true,
            remoteValueMapper.Input);
        Assert.Same(
            mappedValue,
            result.ReturnValue);
        Assert.False(
            result.HasDiagnostic);
    }

    [Fact]
    public void Map_SuccessWithoutReturnValue_ShouldPreserveAbsence()
    {
        var remoteValueMapper =
            CreateRemoteValueMapper();
        var mapper =
            new RuntimeHostCommandOperationResultMapper(
                CreateStatusMapper(),
                remoteValueMapper);

        GrpcV1.CommandOperationResult result =
            mapper.Map(
                Northbound.RuntimeHostCommandOperationResult.Successful());

        Assert.Null(
            remoteValueMapper.Input);
        Assert.Null(
            result.ReturnValue);
        Assert.False(
            result.HasDiagnostic);
    }

    [Fact]
    public void Map_Failure_ShouldMapStatusDiagnosticAndValueAbsence()
    {
        var statusMapper =
            new TestStatusMapper(
                GrpcV1.CommandOperationStatus.EndpointRejected);
        var remoteValueMapper =
            CreateRemoteValueMapper();
        var mapper =
            new RuntimeHostCommandOperationResultMapper(
                statusMapper,
                remoteValueMapper);

        GrpcV1.CommandOperationResult result =
            mapper.Map(
                Northbound.RuntimeHostCommandOperationResult.Failed(
                    Northbound.RuntimeHostCommandOperationStatus.EndpointRejected,
                    "Endpoint rejected the Command."));

        Assert.Equal(
            Northbound.RuntimeHostCommandOperationStatus.EndpointRejected,
            statusMapper.Input);
        Assert.Equal(
            GrpcV1.CommandOperationStatus.EndpointRejected,
            result.Status);
        Assert.Null(
            remoteValueMapper.Input);
        Assert.Null(
            result.ReturnValue);
        Assert.True(
            result.HasDiagnostic);
        Assert.Equal(
            "Endpoint rejected the Command.",
            result.Diagnostic);
    }

    [Fact]
    public void Map_RemoteValueMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new RuntimeHostCommandOperationResultMapper(
                CreateStatusMapper(),
                new TestRemoteValueMapper(
                    null!));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        Northbound.RuntimeHostCommandOperationResult.Successful(
                            true)));

        Assert.Equal(
            "The remote value mapper returned null.",
            exception.Message);
    }

    private static RuntimeHostCommandOperationResultMapper CreateMapper()
    {
        return new RuntimeHostCommandOperationResultMapper(
            CreateStatusMapper(),
            CreateRemoteValueMapper());
    }

    private static TestStatusMapper CreateStatusMapper()
    {
        return new TestStatusMapper(
            GrpcV1.CommandOperationStatus.Success);
    }

    private static TestRemoteValueMapper CreateRemoteValueMapper()
    {
        return new TestRemoteValueMapper(
            new GrpcV1.RemoteValue());
    }

    private sealed class TestStatusMapper
        : IRuntimeHostCommandOperationStatusMapper
    {
        private readonly GrpcV1.CommandOperationStatus result;

        public TestStatusMapper(
            GrpcV1.CommandOperationStatus result)
        {
            this.result =
                result;
        }

        public Northbound.RuntimeHostCommandOperationStatus? Input
        {
            get;
            private set;
        }

        public GrpcV1.CommandOperationStatus Map(
            Northbound.RuntimeHostCommandOperationStatus status)
        {
            Input =
                status;

            return result;
        }
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

        public object? MapToClr(
            GrpcV1.RemoteValue value)
        {
            throw new NotSupportedException();
        }
    }
}
