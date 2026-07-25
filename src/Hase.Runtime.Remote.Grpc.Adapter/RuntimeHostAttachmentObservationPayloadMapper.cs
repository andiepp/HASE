using Google.Protobuf.WellKnownTypes;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized attachment lifecycle observation payloads to the version 1
/// remote contract.
/// </summary>
public sealed class RuntimeHostAttachmentObservationPayloadMapper
    : IRuntimeHostAttachmentObservationPayloadMapper
{
    private readonly IRuntimeEndpointSnapshotMapper endpointSnapshotMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public RuntimeHostAttachmentObservationPayloadMapper(
        IRuntimeEndpointSnapshotMapper endpointSnapshotMapper)
    {
        this.endpointSnapshotMapper =
            endpointSnapshotMapper
            ?? throw new ArgumentNullException(
                nameof(endpointSnapshotMapper));
    }

    /// <inheritdoc />
    public GrpcV1.AttachmentPublishedObservation Map(
        Northbound.RuntimeHostAttachmentPublishedObservationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(
            payload);

        return new GrpcV1.AttachmentPublishedObservation
        {
            Endpoint =
                endpointSnapshotMapper.Map(
                    payload.Endpoint)
                ?? throw new InvalidOperationException(
                    "The endpoint snapshot mapper returned null.")
        };
    }

    /// <inheritdoc />
    public GrpcV1.AttachmentEndedObservation Map(
        Northbound.RuntimeHostAttachmentEndedObservationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(
            payload);

        return new GrpcV1.AttachmentEndedObservation
        {
            EndedAtUtc =
                Timestamp.FromDateTimeOffset(
                    payload.EndedAtUtc)
        };
    }
}
