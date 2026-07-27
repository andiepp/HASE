using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Client.Grpc;

/// <summary>
/// Maps normalized Command operations to and from gRPC version 1.
/// </summary>
public sealed class RuntimeHostGrpcCommandMapper
{
    public GrpcV1.ExecuteCommandRequest MapRequest(
        RemoteCommandExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        var result =
            new GrpcV1.ExecuteCommandRequest
            {
                Target =
                    new GrpcV1.CommandTarget
                    {
                        EndpointId =
                            request.Target.EndpointId.Value,
                        AttachmentGeneration =
                            request.Target.AttachmentGeneration.ToString(),
                        InstrumentId =
                            request.Target.InstrumentId.Value
                    }
            };
        result.Target.CommandPathSegments.AddRange(
            request.Target.CommandPath.Segments);

        if (request.Argument is not null)
        {
            throw new NotSupportedException(
                "This client increment supports only parameterless Commands.");
        }

        return result;
    }

    public RemoteCommandOperationResult MapResult(
        GrpcV1.CommandOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        RemoteCommandOperationStatus status =
            result.Status switch
            {
                GrpcV1.CommandOperationStatus.Success =>
                    RemoteCommandOperationStatus.Success,
                GrpcV1.CommandOperationStatus.AttachmentNotCurrent =>
                    RemoteCommandOperationStatus.AttachmentNotCurrent,
                GrpcV1.CommandOperationStatus.InstrumentNotFound =>
                    RemoteCommandOperationStatus.InstrumentNotFound,
                GrpcV1.CommandOperationStatus.CommandNotFound =>
                    RemoteCommandOperationStatus.CommandNotFound,
                GrpcV1.CommandOperationStatus.ArgumentNotSupported =>
                    RemoteCommandOperationStatus.ArgumentNotSupported,
                GrpcV1.CommandOperationStatus.EndpointUnavailable =>
                    RemoteCommandOperationStatus.EndpointUnavailable,
                GrpcV1.CommandOperationStatus.EndpointRejected =>
                    RemoteCommandOperationStatus.EndpointRejected,
                GrpcV1.CommandOperationStatus.EndpointFailure =>
                    RemoteCommandOperationStatus.EndpointFailure,
                GrpcV1.CommandOperationStatus.TimedOut =>
                    RemoteCommandOperationStatus.TimedOut,
                _ =>
                    throw new InvalidDataException(
                        "The Command operation has an unsupported status.")
            };

        if (status != RemoteCommandOperationStatus.Success)
        {
            return RemoteCommandOperationResult.Failed(
                status,
                result.HasDiagnostic
                    ? result.Diagnostic
                    : null);
        }

        return RemoteCommandOperationResult.Successful(
            result.ReturnValue is null
                ? null
                : MapValue(
                    result.ReturnValue));
    }

    private static RemoteValue MapValue(
        GrpcV1.RemoteValue value) =>
        value.KindCase switch
        {
            GrpcV1.RemoteValue.KindOneofCase.BooleanValue =>
                RemoteValue.FromBoolean(
                    value.BooleanValue),
            GrpcV1.RemoteValue.KindOneofCase.StringValue =>
                RemoteValue.FromString(
                    value.StringValue),
            GrpcV1.RemoteValue.KindOneofCase.NumericValue =>
                RemoteValue.FromNumeric(
                    value.NumericValue),
            _ =>
                throw new InvalidDataException(
                    "The Command return value has no supported kind.")
        };
}
