using Hase.Runtime.Connections;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;
using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class
    RuntimeHostConnectionStatusChangedObservationPayloadMapperTests
{
    [Fact]
    public void Constructor_NullConnectionStatusMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "connectionStatusMapper",
            () =>
                new RuntimeHostConnectionStatusChangedObservationPayloadMapper(
                    null!));
    }

    [Fact]
    public void Map_NullPayload_ShouldThrow()
    {
        var mapper =
            CreateMapper();

        Assert.Throws<ArgumentNullException>(
            "payload",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_DefinedPayload_ShouldMapPreviousAndCurrentStatus()
    {
        var previousStatus =
            new EndpointConnectionStatus(
                EndpointConnectionState.Connecting);
        var currentStatus =
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready);
        var mappedPrevious =
            new GrpcV1.EndpointConnectionStatus
            {
                State =
                    GrpcV1.EndpointConnectionState.Connecting
            };
        var mappedCurrent =
            new GrpcV1.EndpointConnectionStatus
            {
                State =
                    GrpcV1.EndpointConnectionState.Ready
            };
        var statusMapper =
            new TestConnectionStatusMapper(
                mappedPrevious,
                mappedCurrent);
        var mapper =
            new RuntimeHostConnectionStatusChangedObservationPayloadMapper(
                statusMapper);

        GrpcV1.ConnectionStatusChangedObservation result =
            mapper.Map(
                new Northbound
                    .RuntimeHostConnectionStatusChangedObservationPayload(
                        previousStatus,
                        currentStatus));

        Assert.Equal(
            new[]
            {
                previousStatus,
                currentStatus
            },
            statusMapper.Inputs);
        Assert.Same(
            mappedPrevious,
            result.PreviousStatus);
        Assert.Same(
            mappedCurrent,
            result.CurrentStatus);
    }

    [Fact]
    public void Map_PreviousStatusMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new RuntimeHostConnectionStatusChangedObservationPayloadMapper(
                new TestConnectionStatusMapper(
                    null!,
                    new GrpcV1.EndpointConnectionStatus()));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreatePayload()));

        Assert.Equal(
            "The previous endpoint connection status mapper returned null.",
            exception.Message);
    }

    [Fact]
    public void Map_CurrentStatusMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new RuntimeHostConnectionStatusChangedObservationPayloadMapper(
                new TestConnectionStatusMapper(
                    new GrpcV1.EndpointConnectionStatus(),
                    null!));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreatePayload()));

        Assert.Equal(
            "The current endpoint connection status mapper returned null.",
            exception.Message);
    }

    private static RuntimeHostConnectionStatusChangedObservationPayloadMapper
        CreateMapper()
    {
        return new RuntimeHostConnectionStatusChangedObservationPayloadMapper(
            new TestConnectionStatusMapper(
                new GrpcV1.EndpointConnectionStatus(),
                new GrpcV1.EndpointConnectionStatus()));
    }

    private static
        Northbound.RuntimeHostConnectionStatusChangedObservationPayload
        CreatePayload()
    {
        return new Northbound
            .RuntimeHostConnectionStatusChangedObservationPayload(
                new EndpointConnectionStatus(
                    EndpointConnectionState.Connecting),
                new EndpointConnectionStatus(
                    EndpointConnectionState.Ready));
    }

    private sealed class TestConnectionStatusMapper
        : IEndpointConnectionStatusMapper
    {
        private readonly Queue<GrpcV1.EndpointConnectionStatus> results;

        public TestConnectionStatusMapper(
            GrpcV1.EndpointConnectionStatus first,
            GrpcV1.EndpointConnectionStatus second)
        {
            results =
                new Queue<GrpcV1.EndpointConnectionStatus>(
                    new[]
                    {
                        first,
                        second
                    });
        }

        public List<EndpointConnectionStatus> Inputs
        {
            get;
        } =
            [];

        public GrpcV1.EndpointConnectionStatus Map(
            EndpointConnectionStatus status)
        {
            Inputs.Add(
                status);

            return results.Dequeue();
        }
    }
}
