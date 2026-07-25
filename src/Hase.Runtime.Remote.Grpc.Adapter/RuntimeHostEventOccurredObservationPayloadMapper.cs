using Google.Protobuf.WellKnownTypes;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized Event-occurred observation payloads to the version 1 remote
/// contract.
/// </summary>
public sealed class RuntimeHostEventOccurredObservationPayloadMapper
    : IRuntimeHostEventOccurredObservationPayloadMapper
{
    private readonly IRemoteValueMapper remoteValueMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public RuntimeHostEventOccurredObservationPayloadMapper(
        IRemoteValueMapper remoteValueMapper)
    {
        this.remoteValueMapper =
            remoteValueMapper
            ?? throw new ArgumentNullException(
                nameof(remoteValueMapper));
    }

    /// <inheritdoc />
    public GrpcV1.EventOccurredObservation Map(
        Northbound.RuntimeHostEventOccurredObservationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(
            payload);

        var result =
            new GrpcV1.EventOccurredObservation
            {
                InstrumentId =
                    payload.InstrumentId.Value,
                OccurredAtUtc =
                    Timestamp.FromDateTimeOffset(
                        payload.OccurredAtUtc)
            };

        result.EventPathSegments.Add(
            payload.EventPath.Segments);

        if (payload.Value is not null)
        {
            result.Value =
                remoteValueMapper.Map(
                    payload.Value)
                ?? throw new InvalidOperationException(
                    "The remote Event value mapper returned null.");
        }

        return result;
    }
}
