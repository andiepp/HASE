using Google.Protobuf.WellKnownTypes;
using RuntimeConnections = global::Hase.Runtime.Connections;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class EndpointConnectionStatusMapperTests
{
    [Fact]
    public void Map_NullStatus_ShouldThrow()
    {
        var mapper =
            new EndpointConnectionStatusMapper();

        Assert.Throws<ArgumentNullException>(
            "status",
            () =>
                mapper.Map(
                    null!));
    }

    [Theory]
    [InlineData(RuntimeConnections.EndpointConnectionState.Disconnected, 1)]
    [InlineData(RuntimeConnections.EndpointConnectionState.Connecting, 2)]
    [InlineData(RuntimeConnections.EndpointConnectionState.Synchronizing, 3)]
    [InlineData(RuntimeConnections.EndpointConnectionState.Ready, 4)]
    [InlineData(RuntimeConnections.EndpointConnectionState.Reconnecting, 5)]
    [InlineData(RuntimeConnections.EndpointConnectionState.Faulted, 6)]
    public void Map_State_ShouldUseStableRemoteValue(
        RuntimeConnections.EndpointConnectionState source,
        int expectedRemoteValue)
    {
        var mapper =
            new EndpointConnectionStatusMapper();

        GrpcV1.EndpointConnectionStatus result =
            mapper.Map(
                new RuntimeConnections.EndpointConnectionStatus(
                    source));

        Assert.Equal(
            expectedRemoteValue,
            (int)result.State);
        Assert.Null(
            result.ChangedAtUtc);
        Assert.False(
            result.HasDetail);
    }

    [Fact]
    public void Map_OptionalMembers_ShouldPreserveUtcTimestampAndDetail()
    {
        var changedAtUtc =
            new DateTimeOffset(
                2026,
                7,
                25,
                12,
                30,
                45,
                TimeSpan.Zero);

        var mapper =
            new EndpointConnectionStatusMapper();

        GrpcV1.EndpointConnectionStatus result =
            mapper.Map(
                new RuntimeConnections.EndpointConnectionStatus(
                    RuntimeConnections.EndpointConnectionState.Ready,
                    changedAtUtc,
                    "Endpoint synchronized."));

        Assert.Equal(
            Timestamp.FromDateTimeOffset(
                changedAtUtc),
            result.ChangedAtUtc);
        Assert.True(
            result.HasDetail);
        Assert.Equal(
            "Endpoint synchronized.",
            result.Detail);
    }

    [Fact]
    public void Map_UnknownState_ShouldThrow()
    {
        const RuntimeConnections.EndpointConnectionState unknownState =
            (RuntimeConnections.EndpointConnectionState)99;

        var mapper =
            new EndpointConnectionStatusMapper();

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                "state",
                () =>
                    mapper.Map(
                        new RuntimeConnections.EndpointConnectionStatus(
                            unknownState)));

        Assert.Equal(
            unknownState,
            exception.ActualValue);
    }
}
