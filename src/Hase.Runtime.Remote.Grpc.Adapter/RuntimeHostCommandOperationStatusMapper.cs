using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized northbound Command operation statuses to the version 1
/// remote contract.
/// </summary>
public sealed class RuntimeHostCommandOperationStatusMapper
    : IRuntimeHostCommandOperationStatusMapper
{
    /// <inheritdoc />
    public GrpcV1.CommandOperationStatus Map(
        Northbound.RuntimeHostCommandOperationStatus status)
    {
        return status switch
        {
            Northbound.RuntimeHostCommandOperationStatus.Success =>
                GrpcV1.CommandOperationStatus.Success,
            Northbound.RuntimeHostCommandOperationStatus.AttachmentNotCurrent =>
                GrpcV1.CommandOperationStatus.AttachmentNotCurrent,
            Northbound.RuntimeHostCommandOperationStatus.InstrumentNotFound =>
                GrpcV1.CommandOperationStatus.InstrumentNotFound,
            Northbound.RuntimeHostCommandOperationStatus.CommandNotFound =>
                GrpcV1.CommandOperationStatus.CommandNotFound,
            Northbound.RuntimeHostCommandOperationStatus.ArgumentNotSupported =>
                GrpcV1.CommandOperationStatus.ArgumentNotSupported,
            Northbound.RuntimeHostCommandOperationStatus.EndpointUnavailable =>
                GrpcV1.CommandOperationStatus.EndpointUnavailable,
            Northbound.RuntimeHostCommandOperationStatus.EndpointRejected =>
                GrpcV1.CommandOperationStatus.EndpointRejected,
            Northbound.RuntimeHostCommandOperationStatus.EndpointFailure =>
                GrpcV1.CommandOperationStatus.EndpointFailure,
            Northbound.RuntimeHostCommandOperationStatus.TimedOut =>
                GrpcV1.CommandOperationStatus.TimedOut,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    status,
                    "The Command operation status is not supported.")
        };
    }
}
