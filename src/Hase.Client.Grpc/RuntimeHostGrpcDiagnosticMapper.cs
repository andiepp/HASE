using Google.Protobuf.WellKnownTypes;
using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Client.Grpc;

/// <summary>Strictly maps the version 1 diagnostic projection to Hase.Client.</summary>
public sealed class RuntimeHostGrpcDiagnosticMapper
{
    public RemoteRuntimeDiagnosticObservation Map(
        GrpcV1.ProjectedDiagnosticObservation source)
    {
        ArgumentNullException.ThrowIfNull(source);
        GrpcV1.ProjectedDiagnosticRecord record = source.Record
            ?? throw new InvalidDataException(
                "The projected diagnostic observation has no record.");

        return new RemoteRuntimeDiagnosticObservation(
            checked((long)source.Sequence),
            new RemoteRuntimeDiagnosticRecord(
                record.RuntimeHostId,
                checked((long)record.SourceSequence),
                MapTimestamp(record.TimestampUtc),
                MapLevel(record.Level),
                MapCategory(record.Category),
                record.EventName,
                MapSeverity(record.Severity),
                record.HasEndpointId ? record.EndpointId : null,
                record.HasAttachmentGeneration
                    ? ParseGuid(record.AttachmentGeneration, "attachment generation")
                    : null,
                record.HasDirection ? MapDirection(record.Direction) : null,
                record.HasOperationId
                    ? ParseGuid(record.OperationId, "operation identity")
                    : null,
                record.Duration is null ? null : MapDuration(record.Duration),
                record.HasOutcome ? MapOutcome(record.Outcome) : null,
                record.Details,
                record.ByteSnapshot is null
                    ? null
                    : MapBytes(record.ByteSnapshot)));
    }

    private static DateTimeOffset MapTimestamp(Timestamp? value)
    {
        if (value is null)
        {
            throw new InvalidDataException(
                "The projected diagnostic record has no UTC timestamp.");
        }
        try
        {
            return value.ToDateTimeOffset();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException(
                "The projected diagnostic UTC timestamp is invalid.",
                exception);
        }
    }

    private static TimeSpan MapDuration(Duration value)
    {
        try
        {
            return value.ToTimeSpan();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException(
                "The projected diagnostic duration is invalid.",
                exception);
        }
    }

    private static Guid ParseGuid(string value, string role) =>
        Guid.TryParseExact(value, "D", out Guid result)
            ? result
            : throw new InvalidDataException(
                $"The projected diagnostic {role} is invalid.");

    private static RemoteRuntimeDiagnosticByteSnapshot MapBytes(
        GrpcV1.ProjectedDiagnosticByteSnapshot value) =>
        new(
            checked((int)value.OriginalByteCount),
            value.CapturedBytes.Span,
            value.IsTruncated);

    private static RemoteRuntimeDiagnosticLevel MapLevel(
        GrpcV1.RuntimeDiagnosticLevel value) => value switch
        {
            GrpcV1.RuntimeDiagnosticLevel.Operational =>
                RemoteRuntimeDiagnosticLevel.Operational,
            GrpcV1.RuntimeDiagnosticLevel.Protocol =>
                RemoteRuntimeDiagnosticLevel.Protocol,
            GrpcV1.RuntimeDiagnosticLevel.Bytes =>
                RemoteRuntimeDiagnosticLevel.Bytes,
            _ => throw Unsupported("level", value)
        };

    private static RemoteRuntimeDiagnosticCategory MapCategory(
        GrpcV1.RuntimeDiagnosticCategory value) => value switch
        {
            GrpcV1.RuntimeDiagnosticCategory.RuntimeAttachment => RemoteRuntimeDiagnosticCategory.RuntimeAttachment,
            GrpcV1.RuntimeDiagnosticCategory.RuntimeConnection => RemoteRuntimeDiagnosticCategory.RuntimeConnection,
            GrpcV1.RuntimeDiagnosticCategory.RuntimeSynchronization => RemoteRuntimeDiagnosticCategory.RuntimeSynchronization,
            GrpcV1.RuntimeDiagnosticCategory.RuntimeRecovery => RemoteRuntimeDiagnosticCategory.RuntimeRecovery,
            GrpcV1.RuntimeDiagnosticCategory.RuntimeProperty => RemoteRuntimeDiagnosticCategory.RuntimeProperty,
            GrpcV1.RuntimeDiagnosticCategory.RuntimeCommand => RemoteRuntimeDiagnosticCategory.RuntimeCommand,
            GrpcV1.RuntimeDiagnosticCategory.RuntimeEvent => RemoteRuntimeDiagnosticCategory.RuntimeEvent,
            GrpcV1.RuntimeDiagnosticCategory.ProtocolExchange => RemoteRuntimeDiagnosticCategory.ProtocolExchange,
            GrpcV1.RuntimeDiagnosticCategory.TransportBytes => RemoteRuntimeDiagnosticCategory.TransportBytes,
            _ => throw Unsupported("category", value)
        };

    private static RemoteRuntimeDiagnosticSeverity MapSeverity(
        GrpcV1.RuntimeDiagnosticSeverity value) => value switch
        {
            GrpcV1.RuntimeDiagnosticSeverity.Trace => RemoteRuntimeDiagnosticSeverity.Trace,
            GrpcV1.RuntimeDiagnosticSeverity.Information => RemoteRuntimeDiagnosticSeverity.Information,
            GrpcV1.RuntimeDiagnosticSeverity.Warning => RemoteRuntimeDiagnosticSeverity.Warning,
            GrpcV1.RuntimeDiagnosticSeverity.Error => RemoteRuntimeDiagnosticSeverity.Error,
            _ => throw Unsupported("severity", value)
        };

    private static RemoteRuntimeDiagnosticDirection MapDirection(
        GrpcV1.RuntimeDiagnosticDirection value) => value switch
        {
            GrpcV1.RuntimeDiagnosticDirection.Outbound => RemoteRuntimeDiagnosticDirection.Outbound,
            GrpcV1.RuntimeDiagnosticDirection.Inbound => RemoteRuntimeDiagnosticDirection.Inbound,
            _ => throw Unsupported("direction", value)
        };

    private static RemoteRuntimeDiagnosticOutcome MapOutcome(
        GrpcV1.RuntimeDiagnosticOutcome value) => value switch
        {
            GrpcV1.RuntimeDiagnosticOutcome.Succeeded => RemoteRuntimeDiagnosticOutcome.Succeeded,
            GrpcV1.RuntimeDiagnosticOutcome.Failed => RemoteRuntimeDiagnosticOutcome.Failed,
            GrpcV1.RuntimeDiagnosticOutcome.Cancelled => RemoteRuntimeDiagnosticOutcome.Cancelled,
            GrpcV1.RuntimeDiagnosticOutcome.TimedOut => RemoteRuntimeDiagnosticOutcome.TimedOut,
            _ => throw Unsupported("outcome", value)
        };

    private static InvalidDataException Unsupported<T>(string role, T value) =>
        new($"The projected diagnostic {role} '{value}' is unsupported.");
}
