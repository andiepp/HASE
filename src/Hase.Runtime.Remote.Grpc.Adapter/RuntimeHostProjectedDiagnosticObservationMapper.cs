using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using RuntimeDiagnostics = global::Hase.Runtime.Diagnostics;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one already-sanitized Runtime Host diagnostic projection observation
/// to the version 1 remote representation.
/// </summary>
public sealed class RuntimeHostProjectedDiagnosticObservationMapper
{
    public GrpcV1.ProjectedDiagnosticObservation Map(
        Northbound.RuntimeHostProjectedDiagnosticObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        Northbound.RuntimeHostProjectedDiagnosticRecord source =
            observation.Record;
        var record = new GrpcV1.ProjectedDiagnosticRecord
        {
            RuntimeHostId = source.RuntimeHostId.Value,
            SourceSequence = checked((ulong)source.SourceSequence),
            TimestampUtc = Timestamp.FromDateTimeOffset(source.TimestampUtc),
            Level = MapLevel(source.Level),
            Category = MapCategory(source.Category),
            EventName = source.EventName,
            Severity = MapSeverity(source.Severity)
        };

        if (source.EndpointId is not null)
        {
            record.EndpointId = source.EndpointId;
        }

        if (source.AttachmentGeneration.HasValue)
        {
            record.AttachmentGeneration =
                source.AttachmentGeneration.Value.ToString("D");
        }

        if (source.Direction.HasValue)
        {
            record.Direction = MapDirection(source.Direction.Value);
        }

        if (source.OperationId.HasValue)
        {
            record.OperationId = source.OperationId.Value.ToString("D");
        }

        if (source.Duration.HasValue)
        {
            record.Duration = Duration.FromTimeSpan(source.Duration.Value);
        }

        if (source.Outcome.HasValue)
        {
            record.Outcome = MapOutcome(source.Outcome.Value);
        }

        foreach (KeyValuePair<string, string> detail in source.Details)
        {
            record.Details.Add(detail.Key, detail.Value);
        }

        if (source.ByteSnapshot is not null)
        {
            record.ByteSnapshot = new GrpcV1.ProjectedDiagnosticByteSnapshot
            {
                OriginalByteCount =
                    checked((ulong)source.ByteSnapshot.OriginalByteCount),
                CapturedBytes = ByteString.CopyFrom(
                    source.ByteSnapshot.ToArray()),
                IsTruncated = source.ByteSnapshot.IsTruncated
            };
        }

        return new GrpcV1.ProjectedDiagnosticObservation
        {
            Sequence = checked((ulong)observation.Sequence.Value),
            Record = record
        };
    }

    private static GrpcV1.RuntimeDiagnosticLevel MapLevel(
        RuntimeDiagnostics.RuntimeDiagnosticLevel level)
    {
        return level switch
        {
            RuntimeDiagnostics.RuntimeDiagnosticLevel.Operational =>
                GrpcV1.RuntimeDiagnosticLevel.Operational,
            RuntimeDiagnostics.RuntimeDiagnosticLevel.Protocol =>
                GrpcV1.RuntimeDiagnosticLevel.Protocol,
            RuntimeDiagnostics.RuntimeDiagnosticLevel.Bytes =>
                GrpcV1.RuntimeDiagnosticLevel.Bytes,
            _ => throw Unsupported(nameof(level), level)
        };
    }

    private static GrpcV1.RuntimeDiagnosticCategory MapCategory(
        RuntimeDiagnostics.RuntimeDiagnosticCategory category)
    {
        return category switch
        {
            RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeAttachment =>
                GrpcV1.RuntimeDiagnosticCategory.RuntimeAttachment,
            RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeConnection =>
                GrpcV1.RuntimeDiagnosticCategory.RuntimeConnection,
            RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeSynchronization =>
                GrpcV1.RuntimeDiagnosticCategory.RuntimeSynchronization,
            RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeRecovery =>
                GrpcV1.RuntimeDiagnosticCategory.RuntimeRecovery,
            RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeProperty =>
                GrpcV1.RuntimeDiagnosticCategory.RuntimeProperty,
            RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeCommand =>
                GrpcV1.RuntimeDiagnosticCategory.RuntimeCommand,
            RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeEvent =>
                GrpcV1.RuntimeDiagnosticCategory.RuntimeEvent,
            RuntimeDiagnostics.RuntimeDiagnosticCategory.ProtocolExchange =>
                GrpcV1.RuntimeDiagnosticCategory.ProtocolExchange,
            RuntimeDiagnostics.RuntimeDiagnosticCategory.TransportBytes =>
                GrpcV1.RuntimeDiagnosticCategory.TransportBytes,
            _ => throw Unsupported(nameof(category), category)
        };
    }

    private static GrpcV1.RuntimeDiagnosticSeverity MapSeverity(
        RuntimeDiagnostics.RuntimeDiagnosticSeverity severity)
    {
        return severity switch
        {
            RuntimeDiagnostics.RuntimeDiagnosticSeverity.Trace =>
                GrpcV1.RuntimeDiagnosticSeverity.Trace,
            RuntimeDiagnostics.RuntimeDiagnosticSeverity.Information =>
                GrpcV1.RuntimeDiagnosticSeverity.Information,
            RuntimeDiagnostics.RuntimeDiagnosticSeverity.Warning =>
                GrpcV1.RuntimeDiagnosticSeverity.Warning,
            RuntimeDiagnostics.RuntimeDiagnosticSeverity.Error =>
                GrpcV1.RuntimeDiagnosticSeverity.Error,
            _ => throw Unsupported(nameof(severity), severity)
        };
    }

    private static GrpcV1.RuntimeDiagnosticDirection MapDirection(
        RuntimeDiagnostics.RuntimeDiagnosticDirection direction)
    {
        return direction switch
        {
            RuntimeDiagnostics.RuntimeDiagnosticDirection.Outbound =>
                GrpcV1.RuntimeDiagnosticDirection.Outbound,
            RuntimeDiagnostics.RuntimeDiagnosticDirection.Inbound =>
                GrpcV1.RuntimeDiagnosticDirection.Inbound,
            _ => throw Unsupported(nameof(direction), direction)
        };
    }

    private static GrpcV1.RuntimeDiagnosticOutcome MapOutcome(
        RuntimeDiagnostics.RuntimeDiagnosticOutcome outcome)
    {
        return outcome switch
        {
            RuntimeDiagnostics.RuntimeDiagnosticOutcome.Succeeded =>
                GrpcV1.RuntimeDiagnosticOutcome.Succeeded,
            RuntimeDiagnostics.RuntimeDiagnosticOutcome.Failed =>
                GrpcV1.RuntimeDiagnosticOutcome.Failed,
            RuntimeDiagnostics.RuntimeDiagnosticOutcome.Cancelled =>
                GrpcV1.RuntimeDiagnosticOutcome.Cancelled,
            RuntimeDiagnostics.RuntimeDiagnosticOutcome.TimedOut =>
                GrpcV1.RuntimeDiagnosticOutcome.TimedOut,
            _ => throw Unsupported(nameof(outcome), outcome)
        };
    }

    private static ArgumentOutOfRangeException Unsupported<T>(
        string parameterName,
        T value)
        where T : struct, System.Enum
    {
        return new ArgumentOutOfRangeException(
            parameterName,
            value,
            "The runtime diagnostic value is not supported.");
    }
}
