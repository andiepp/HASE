using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized connection-status-changed observation payloads to the
/// version 1 remote contract.
/// </summary>
public sealed class RuntimeHostConnectionStatusChangedObservationPayloadMapper
    : IRuntimeHostConnectionStatusChangedObservationPayloadMapper
{
    private readonly IEndpointConnectionStatusMapper connectionStatusMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public RuntimeHostConnectionStatusChangedObservationPayloadMapper(
        IEndpointConnectionStatusMapper connectionStatusMapper)
    {
        this.connectionStatusMapper =
            connectionStatusMapper
            ?? throw new ArgumentNullException(
                nameof(connectionStatusMapper));
    }

    /// <inheritdoc />
    public GrpcV1.ConnectionStatusChangedObservation Map(
        Northbound.RuntimeHostConnectionStatusChangedObservationPayload
            payload)
    {
        ArgumentNullException.ThrowIfNull(
            payload);

        GrpcV1.EndpointConnectionStatus previousStatus =
            connectionStatusMapper.Map(
                payload.PreviousStatus)
            ?? throw new InvalidOperationException(
                "The previous endpoint connection status mapper returned null.");

        GrpcV1.EndpointConnectionStatus currentStatus =
            connectionStatusMapper.Map(
                payload.CurrentStatus)
            ?? throw new InvalidOperationException(
                "The current endpoint connection status mapper returned null.");

        return new GrpcV1.ConnectionStatusChangedObservation
        {
            PreviousStatus =
                previousStatus,
            CurrentStatus =
                currentStatus
        };
    }
}
