using Google.Protobuf.WellKnownTypes;
using RuntimeConnections = global::Hase.Runtime.Connections;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps runtime endpoint connection status to the version 1 remote contract.
/// </summary>
public sealed class EndpointConnectionStatusMapper
    : IEndpointConnectionStatusMapper
{
    /// <inheritdoc />
    public GrpcV1.EndpointConnectionStatus Map(
        RuntimeConnections.EndpointConnectionStatus status)
    {
        ArgumentNullException.ThrowIfNull(
            status);

        var result =
            new GrpcV1.EndpointConnectionStatus
            {
                State =
                    MapState(
                        status.State)
            };

        if (status.ChangedAtUtc.HasValue)
        {
            result.ChangedAtUtc =
                Timestamp.FromDateTimeOffset(
                    status.ChangedAtUtc.Value);
        }

        if (status.Detail is not null)
        {
            result.Detail =
                status.Detail;
        }

        return result;
    }

    private static GrpcV1.EndpointConnectionState MapState(
        RuntimeConnections.EndpointConnectionState state)
    {
        return state switch
        {
            RuntimeConnections.EndpointConnectionState.Disconnected =>
                GrpcV1.EndpointConnectionState.Disconnected,
            RuntimeConnections.EndpointConnectionState.Connecting =>
                GrpcV1.EndpointConnectionState.Connecting,
            RuntimeConnections.EndpointConnectionState.Synchronizing =>
                GrpcV1.EndpointConnectionState.Synchronizing,
            RuntimeConnections.EndpointConnectionState.Ready =>
                GrpcV1.EndpointConnectionState.Ready,
            RuntimeConnections.EndpointConnectionState.Reconnecting =>
                GrpcV1.EndpointConnectionState.Reconnecting,
            RuntimeConnections.EndpointConnectionState.Faulted =>
                GrpcV1.EndpointConnectionState.Faulted,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(state),
                    state,
                    "The endpoint connection state is not supported.")
        };
    }
}
