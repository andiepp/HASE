using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;
using Hase.Core.Domain.Data;

namespace Hase.Client.Grpc;

/// <summary>
/// Maps authoritative Property operations between normalized client contracts
/// and the version-1 gRPC contract.
/// </summary>
public sealed class RuntimeHostGrpcPropertyMapper
{
    public GrpcV1.ReadAuthoritativePropertyRequest MapRequest(
        RemotePropertyTarget target)
    {
        ArgumentNullException.ThrowIfNull(
            target);

        return new GrpcV1.ReadAuthoritativePropertyRequest
        {
            Target =
                MapTarget(
                    target)
        };
    }

    public GrpcV1.WritePropertyRequest MapWriteRequest(
        RemotePropertyTarget target,
        RemoteValue requestedValue)
    {
        ArgumentNullException.ThrowIfNull(
            target);
        ArgumentNullException.ThrowIfNull(
            requestedValue);

        return new GrpcV1.WritePropertyRequest
        {
            Target =
                MapTarget(
                    target),
            RequestedValue =
                MapValue(
                    requestedValue)
        };
    }

    public RemotePropertyOperationResult MapResult(
        GrpcV1.PropertyOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        RemotePropertyOperationStatus status =
            MapStatus(
                result.Status);

        if (status != RemotePropertyOperationStatus.Success)
        {
            return RemotePropertyOperationResult.Failed(
                status,
                result.HasDiagnostic
                    ? result.Diagnostic
                    : null);
        }

        GrpcV1.PropertyValue confirmedValue =
            result.ConfirmedValue
            ?? throw new InvalidDataException(
                "A successful Property operation has no confirmed value.");

        return RemotePropertyOperationResult.Successful(
            new RemotePropertyValue(
                confirmedValue.Value is null
                    ? null
                    : MapValue(
                        confirmedValue.Value),
                MapTimestamp(
                    confirmedValue.TimestampUtc),
                confirmedValue.Quality switch
                {
                    GrpcV1.PropertyQuality.Good =>
                        RemotePropertyQuality.Good,
                    GrpcV1.PropertyQuality.Uncertain =>
                        RemotePropertyQuality.Uncertain,
                    GrpcV1.PropertyQuality.Bad =>
                        RemotePropertyQuality.Bad,
                    _ =>
                        throw new InvalidDataException(
                            "The confirmed Property value has an "
                            + "unsupported quality.")
                }));
    }

    private static RemotePropertyOperationStatus MapStatus(
        GrpcV1.PropertyOperationStatus status) =>
        status switch
        {
            GrpcV1.PropertyOperationStatus.Success =>
                RemotePropertyOperationStatus.Success,
            GrpcV1.PropertyOperationStatus.AttachmentNotCurrent =>
                RemotePropertyOperationStatus.AttachmentNotCurrent,
            GrpcV1.PropertyOperationStatus.InstrumentNotFound =>
                RemotePropertyOperationStatus.InstrumentNotFound,
            GrpcV1.PropertyOperationStatus.PropertyNotFound =>
                RemotePropertyOperationStatus.PropertyNotFound,
            GrpcV1.PropertyOperationStatus.ReadNotSupported =>
                RemotePropertyOperationStatus.ReadNotSupported,
            GrpcV1.PropertyOperationStatus.WriteNotSupported =>
                RemotePropertyOperationStatus.WriteNotSupported,
            GrpcV1.PropertyOperationStatus.InvalidValue =>
                RemotePropertyOperationStatus.InvalidValue,
            GrpcV1.PropertyOperationStatus.EndpointUnavailable =>
                RemotePropertyOperationStatus.EndpointUnavailable,
            GrpcV1.PropertyOperationStatus.EndpointRejected =>
                RemotePropertyOperationStatus.EndpointRejected,
            GrpcV1.PropertyOperationStatus.EndpointFailure =>
                RemotePropertyOperationStatus.EndpointFailure,
            GrpcV1.PropertyOperationStatus.TimedOut =>
                RemotePropertyOperationStatus.TimedOut,
            _ =>
                throw new InvalidDataException(
                    "The Property operation has an unsupported status.")
        };

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
            GrpcV1.RemoteValue.KindOneofCase.ByteArrayValue =>
                RemoteValue.FromByteArray(
                    new ByteArrayValue(
                        value.ByteArrayValue.ToByteArray())),
            _ =>
                throw new InvalidDataException(
                    "The confirmed remote value has no supported kind.")
        };

    private static GrpcV1.PropertyTarget MapTarget(
        RemotePropertyTarget target) =>
        new()
        {
            EndpointId =
                target.EndpointId.Value,
            AttachmentGeneration =
                target.AttachmentGeneration.ToString(),
            InstrumentId =
                target.InstrumentId.Value,
            PropertyId =
                target.PropertyId.Value
        };

    private static GrpcV1.RemoteValue MapValue(
        RemoteValue value) =>
        value.Kind switch
        {
            RemoteValueKind.Boolean =>
                new GrpcV1.RemoteValue
                {
                    BooleanValue =
                        value.BooleanValue!.Value
                },
            RemoteValueKind.String =>
                new GrpcV1.RemoteValue
                {
                    StringValue =
                        value.StringValue!
                },
            RemoteValueKind.Numeric =>
                new GrpcV1.RemoteValue
                {
                    NumericValue =
                        value.NumericValue!.Value
                },
            RemoteValueKind.ByteArray =>
                new GrpcV1.RemoteValue
                {
                    ByteArrayValue =
                        Google.Protobuf.ByteString.CopyFrom(
                            value.ByteArrayValue!.ToArray())
                },
            _ =>
                throw new InvalidDataException(
                    "The requested remote value has no supported kind.")
        };

    private static DateTimeOffset MapTimestamp(
        Google.Protobuf.WellKnownTypes.Timestamp? timestamp)
    {
        if (timestamp is null)
        {
            throw new InvalidDataException(
                "The confirmed Property value has no UTC timestamp.");
        }

        try
        {
            return timestamp.ToDateTimeOffset();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException(
                "The confirmed Property UTC timestamp is invalid.",
                exception);
        }
    }
}
