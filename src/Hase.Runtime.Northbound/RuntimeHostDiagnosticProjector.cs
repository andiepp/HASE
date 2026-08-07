using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Applies local capture and remote disclosure ceilings and creates immutable
/// projected diagnostic records with default-deny detail filtering.
/// </summary>
public sealed class RuntimeHostDiagnosticProjector
{
    private static readonly HashSet<string> AllowedDetailKeys =
        new(StringComparer.Ordinal)
        {
            "AttemptNumber",
            "CurrentState",
            "DefinitionId",
            "DefinitionVersion",
            "DelayMilliseconds",
            "PreviousState",
            "PropertyCount",
            "RetryIndex",
            "capturedByteCount",
            "correlationId",
            "executionMayHaveOccurred",
            "failureKind",
            "instrument",
            "isTruncated",
            "messageKind",
            "originalByteCount",
            "path",
            "payloadLength",
            "protocolFamily",
            "scpiOutcome"
        };

    private readonly RuntimeHostId runtimeHostId;
    private readonly RuntimeHostDiagnosticProjectionPolicy policy;

    public RuntimeHostDiagnosticProjector(
        RuntimeHostId runtimeHostId,
        RuntimeDiagnosticLevel hostMaximumLevel,
        RuntimeHostDiagnosticProjectionPolicy? policy = null)
    {
        this.runtimeHostId = runtimeHostId
            ?? throw new ArgumentNullException(nameof(runtimeHostId));

        if (!Enum.IsDefined(hostMaximumLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(hostMaximumLevel));
        }

        this.policy = policy ?? new RuntimeHostDiagnosticProjectionPolicy();
        if (this.policy.IsEnabled && this.policy.MaximumLevel > hostMaximumLevel)
        {
            throw new ArgumentException(
                "Remote diagnostic projection cannot exceed the Host capture level.",
                nameof(policy));
        }
    }

    public bool TryProject(
        RuntimeDiagnosticRecord record,
        out RuntimeHostProjectedDiagnosticRecord? projectedRecord)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (!policy.Allows(record.Level))
        {
            projectedRecord = null;
            return false;
        }

        RuntimeHostProjectedDiagnosticByteSnapshot? byteSnapshot =
            record.ByteSnapshot is null
                ? null
                : new RuntimeHostProjectedDiagnosticByteSnapshot(
                    record.ByteSnapshot.OriginalByteCount,
                    record.ByteSnapshot.ToArray(),
                    record.ByteSnapshot.IsTruncated);

        projectedRecord = new RuntimeHostProjectedDiagnosticRecord(
            runtimeHostId,
            record.Sequence,
            record.TimestampUtc,
            record.Level,
            record.Category,
            record.EventName,
            record.Severity,
            record.EndpointId,
            record.AttachmentGeneration,
            record.Direction,
            record.OperationId,
            record.Duration,
            record.Outcome,
            FilterDetails(record.Details),
            byteSnapshot);
        return true;
    }

    public bool IsEnabled(RuntimeDiagnosticLevel level)
    {
        return policy.Allows(level);
    }

    private static IReadOnlyDictionary<string, string> FilterDetails(
        IReadOnlyDictionary<string, string> details)
    {
        return details
            .Where(pair => AllowedDetailKeys.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}
