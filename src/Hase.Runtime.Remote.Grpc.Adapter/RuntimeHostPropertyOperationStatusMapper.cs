using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized northbound Property operation statuses to the version 1
/// remote contract.
/// </summary>
public sealed class RuntimeHostPropertyOperationStatusMapper
    : IRuntimeHostPropertyOperationStatusMapper
{
    /// <inheritdoc />
    public GrpcV1.PropertyOperationStatus Map(
        Northbound.RuntimeHostPropertyOperationStatus status)
    {
        return status switch
        {
            Northbound.RuntimeHostPropertyOperationStatus.Success =>
                GrpcV1.PropertyOperationStatus.Success,
            Northbound.RuntimeHostPropertyOperationStatus.AttachmentNotCurrent =>
                GrpcV1.PropertyOperationStatus.AttachmentNotCurrent,
            Northbound.RuntimeHostPropertyOperationStatus.InstrumentNotFound =>
                GrpcV1.PropertyOperationStatus.InstrumentNotFound,
            Northbound.RuntimeHostPropertyOperationStatus.PropertyNotFound =>
                GrpcV1.PropertyOperationStatus.PropertyNotFound,
            Northbound.RuntimeHostPropertyOperationStatus.ReadNotSupported =>
                GrpcV1.PropertyOperationStatus.ReadNotSupported,
            Northbound.RuntimeHostPropertyOperationStatus.WriteNotSupported =>
                GrpcV1.PropertyOperationStatus.WriteNotSupported,
            Northbound.RuntimeHostPropertyOperationStatus.InvalidValue =>
                GrpcV1.PropertyOperationStatus.InvalidValue,
            Northbound.RuntimeHostPropertyOperationStatus.EndpointUnavailable =>
                GrpcV1.PropertyOperationStatus.EndpointUnavailable,
            Northbound.RuntimeHostPropertyOperationStatus.EndpointRejected =>
                GrpcV1.PropertyOperationStatus.EndpointRejected,
            Northbound.RuntimeHostPropertyOperationStatus.EndpointFailure =>
                GrpcV1.PropertyOperationStatus.EndpointFailure,
            Northbound.RuntimeHostPropertyOperationStatus.TimedOut =>
                GrpcV1.PropertyOperationStatus.TimedOut,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    status,
                    "The Property operation status is not supported.")
        };
    }
}
