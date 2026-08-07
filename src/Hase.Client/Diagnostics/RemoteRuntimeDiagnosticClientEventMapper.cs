using Hase.Client.Configuration;

namespace Hase.Client.Diagnostics;

/// <summary>Maps an already-sanitized Host projection into Client diagnostics.</summary>
public static class RemoteRuntimeDiagnosticClientEventMapper
{
    public static ClientDiagnosticEvent Map(
        RemoteRuntimeDiagnosticRecord source,
        RuntimeHostProfile profile,
        RemoteRuntimeHostId authoritativeRuntimeHostId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(authoritativeRuntimeHostId);

        if (!string.Equals(
                source.RuntimeHostId,
                authoritativeRuntimeHostId.Value,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The projected diagnostic Runtime Host identity does not match the connected session.");
        }

        var metadata = source.Details.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal);
        metadata["RemoteSourceSequence"] = source.SourceSequence.ToString(
            System.Globalization.CultureInfo.InvariantCulture);

        return new ClientDiagnosticEvent(
            MapLevel(source.Level),
            MapCategory(source.Category),
            source.EventName,
            MapSeverity(source.Severity),
            source.Direction.HasValue ? MapDirection(source.Direction.Value) : null,
            source.OperationId,
            source.EndpointId,
            source.AttachmentGeneration,
            duration: source.Duration,
            outcome: source.Outcome.HasValue ? MapOutcome(source.Outcome.Value) : null,
            metadata: metadata,
            sessionContext: new ClientDiagnosticSessionContext(
                profile.ProfileId,
                profile.DisplayName,
                profile.ExpectedRuntimeHostId,
                authoritativeRuntimeHostId),
            byteSnapshot: source.ByteSnapshot);
    }

    private static ClientDiagnosticLevel MapLevel(RemoteRuntimeDiagnosticLevel value) => value switch
    {
        RemoteRuntimeDiagnosticLevel.Operational => ClientDiagnosticLevel.Operational,
        RemoteRuntimeDiagnosticLevel.Protocol => ClientDiagnosticLevel.Protocol,
        RemoteRuntimeDiagnosticLevel.Bytes => ClientDiagnosticLevel.Bytes,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static ClientDiagnosticCategory MapCategory(RemoteRuntimeDiagnosticCategory value) => value switch
    {
        RemoteRuntimeDiagnosticCategory.RuntimeAttachment => ClientDiagnosticCategory.ClientSnapshot,
        RemoteRuntimeDiagnosticCategory.RuntimeConnection => ClientDiagnosticCategory.ClientConnection,
        RemoteRuntimeDiagnosticCategory.RuntimeSynchronization => ClientDiagnosticCategory.ClientObservation,
        RemoteRuntimeDiagnosticCategory.RuntimeRecovery => ClientDiagnosticCategory.ClientRecovery,
        RemoteRuntimeDiagnosticCategory.RuntimeProperty => ClientDiagnosticCategory.ClientProperty,
        RemoteRuntimeDiagnosticCategory.RuntimeCommand => ClientDiagnosticCategory.ClientCommand,
        RemoteRuntimeDiagnosticCategory.RuntimeEvent => ClientDiagnosticCategory.ClientObservation,
        RemoteRuntimeDiagnosticCategory.ProtocolExchange => ClientDiagnosticCategory.NorthboundExchange,
        RemoteRuntimeDiagnosticCategory.TransportBytes => ClientDiagnosticCategory.NorthboundBytes,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static ClientDiagnosticSeverity MapSeverity(RemoteRuntimeDiagnosticSeverity value) => value switch
    {
        RemoteRuntimeDiagnosticSeverity.Trace => ClientDiagnosticSeverity.Trace,
        RemoteRuntimeDiagnosticSeverity.Information => ClientDiagnosticSeverity.Information,
        RemoteRuntimeDiagnosticSeverity.Warning => ClientDiagnosticSeverity.Warning,
        RemoteRuntimeDiagnosticSeverity.Error => ClientDiagnosticSeverity.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static ClientDiagnosticDirection MapDirection(RemoteRuntimeDiagnosticDirection value) => value switch
    {
        RemoteRuntimeDiagnosticDirection.Outbound => ClientDiagnosticDirection.Outbound,
        RemoteRuntimeDiagnosticDirection.Inbound => ClientDiagnosticDirection.Inbound,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static ClientDiagnosticOutcome MapOutcome(RemoteRuntimeDiagnosticOutcome value) => value switch
    {
        RemoteRuntimeDiagnosticOutcome.Succeeded => ClientDiagnosticOutcome.Succeeded,
        RemoteRuntimeDiagnosticOutcome.Failed => ClientDiagnosticOutcome.Failed,
        RemoteRuntimeDiagnosticOutcome.Cancelled => ClientDiagnosticOutcome.Cancelled,
        RemoteRuntimeDiagnosticOutcome.TimedOut => ClientDiagnosticOutcome.TimedOut,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
