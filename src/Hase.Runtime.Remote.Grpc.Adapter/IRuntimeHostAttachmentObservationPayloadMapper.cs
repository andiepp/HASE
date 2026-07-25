using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized attachment lifecycle observation payloads to the version 1
/// remote contract.
/// </summary>
public interface IRuntimeHostAttachmentObservationPayloadMapper
{
    /// <summary>
    /// Maps one attachment-published payload.
    /// </summary>
    GrpcV1.AttachmentPublishedObservation Map(
        Northbound.RuntimeHostAttachmentPublishedObservationPayload payload);

    /// <summary>
    /// Maps one attachment-ended payload.
    /// </summary>
    GrpcV1.AttachmentEndedObservation Map(
        Northbound.RuntimeHostAttachmentEndedObservationPayload payload);
}
