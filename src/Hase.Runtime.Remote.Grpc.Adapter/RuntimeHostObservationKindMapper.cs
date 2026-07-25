using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized northbound observation kinds to the version 1 remote
/// contract.
/// </summary>
public sealed class RuntimeHostObservationKindMapper
    : IRuntimeHostObservationKindMapper
{
    /// <inheritdoc />
    public GrpcV1.RuntimeHostObservationKind Map(
        Northbound.RuntimeHostObservationKind kind)
    {
        return kind switch
        {
            Northbound.RuntimeHostObservationKind.AttachmentPublished =>
                GrpcV1.RuntimeHostObservationKind.AttachmentPublished,
            Northbound.RuntimeHostObservationKind.AttachmentEnded =>
                GrpcV1.RuntimeHostObservationKind.AttachmentEnded,
            Northbound.RuntimeHostObservationKind.ConnectionStatusChanged =>
                GrpcV1.RuntimeHostObservationKind.ConnectionStatusChanged,
            Northbound.RuntimeHostObservationKind.PropertyValueChanged =>
                GrpcV1.RuntimeHostObservationKind.PropertyValueChanged,
            Northbound.RuntimeHostObservationKind.EventOccurred =>
                GrpcV1.RuntimeHostObservationKind.EventOccurred,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "The runtime-host observation kind is not supported.")
        };
    }
}
