using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Hase.Client.Diagnostics;

/// <summary>
/// Adds payload-free diagnostics to one normalized client session.
/// </summary>
public sealed class DiagnosticRuntimeHostClientSession
    : IRuntimeHostClientSession,
      IRuntimeHostPropertyReader,
      IRuntimeHostPropertyWriter,
      IRuntimeHostCommandExecutor,
      IRuntimeHostEventSource
{
    private readonly IRuntimeHostClientSession inner;
    private readonly ClientDiagnosticPublisher diagnostics;
    private bool disposed;

    public DiagnosticRuntimeHostClientSession(
        IRuntimeHostClientSession inner,
        ClientDiagnosticPublisher diagnostics)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        inner.StatusChanged += InnerStatusChanged;

        if (inner is IRuntimeHostEventSource eventSource)
        {
            eventSource.EventOccurred += InnerEventOccurred;
        }
    }

    public event EventHandler<RuntimeHostClientSessionStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<RemoteEventOccurredEventArgs>? EventOccurred;

    public RuntimeHostClientSessionStatus Status => inner.Status;
    public RemoteObservationState? CurrentState => inner.CurrentState;

    public async IAsyncEnumerable<RemoteObservationState> ReadStatesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Guid operationId = Guid.NewGuid();
        Stopwatch duration = Stopwatch.StartNew();
        Publish(
            ClientDiagnosticCategory.ClientLifecycle,
            "SessionStarted",
            operationId: operationId);
        Publish(
            ClientDiagnosticCategory.ClientObservation,
            "ObservationSubscriptionStarted",
            direction: ClientDiagnosticDirection.Outbound,
            operationId: operationId);

        ClientDiagnosticOutcome outcome = ClientDiagnosticOutcome.Succeeded;
        bool firstState = true;

        await using IAsyncEnumerator<RemoteObservationState> states = inner
            .ReadStatesAsync(cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                bool hasValue;
                try
                {
                    hasValue = await states.MoveNextAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    outcome = IsCancellation(exception)
                        ? ClientDiagnosticOutcome.Cancelled
                        : ClientDiagnosticOutcome.Failed;
                    throw;
                }

                if (!hasValue)
                {
                    break;
                }

                RemoteObservationState state = states.Current;
                Publish(
                    ClientDiagnosticCategory.ClientObservation,
                    firstState ? "SnapshotDelivered" : "ObservationStateDelivered",
                    direction: ClientDiagnosticDirection.Inbound,
                    operationId: operationId);

                firstState = false;

                yield return state;
            }
        }
        finally
        {
            duration.Stop();
            Publish(
                ClientDiagnosticCategory.ClientObservation,
                "ObservationSubscriptionEnded",
                operationId: operationId,
                duration: duration.Elapsed,
                outcome: outcome,
                severity: outcome == ClientDiagnosticOutcome.Failed
                    ? ClientDiagnosticSeverity.Error
                    : ClientDiagnosticSeverity.Information);
        }
    }

    public Task<RemotePropertyOperationResult> ReadPropertyAsync(
        RemotePropertyTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (inner is not IRuntimeHostPropertyReader reader)
        {
            throw new NotSupportedException("The client session does not support Property reads.");
        }
        return RunPropertyOperationAsync(
            "PropertyRead",
            target,
            () => reader.ReadPropertyAsync(target, cancellationToken));
    }

    public Task<RemotePropertyOperationResult> WritePropertyAsync(
        RemotePropertyTarget target,
        RemoteValue requestedValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(requestedValue);
        if (inner is not IRuntimeHostPropertyWriter writer)
        {
            throw new NotSupportedException("The client session does not support Property writes.");
        }
        return RunPropertyOperationAsync(
            "PropertyWrite",
            target,
            () => writer.WritePropertyAsync(target, requestedValue, cancellationToken));
    }

    public async Task<RemoteCommandOperationResult> ExecuteCommandAsync(
        RemoteCommandExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(disposed, this);

        if (inner is not IRuntimeHostCommandExecutor executor)
        {
            throw new NotSupportedException("The client session does not support Command execution.");
        }

        Guid operationId = Guid.NewGuid();
        Stopwatch duration = Stopwatch.StartNew();
        RemoteCommandTarget target = request.Target;
        PublishTarget(
            ClientDiagnosticCategory.ClientCommand,
            "CommandExecutionStarted",
            target.EndpointId.Value,
            target.AttachmentGeneration.Value,
            target.InstrumentId.Value,
            target.CommandPath.ToString(),
            operationId,
            ClientDiagnosticDirection.Outbound);

        try
        {
            RemoteCommandOperationResult result =
                await executor.ExecuteCommandAsync(request, cancellationToken).ConfigureAwait(false);
            duration.Stop();
            PublishTarget(
                ClientDiagnosticCategory.ClientCommand,
                "CommandExecutionCompleted",
                target.EndpointId.Value,
                target.AttachmentGeneration.Value,
                target.InstrumentId.Value,
                target.CommandPath.ToString(),
                operationId,
                ClientDiagnosticDirection.Inbound,
                duration.Elapsed,
                result.IsSuccess ? ClientDiagnosticOutcome.Succeeded : ClientDiagnosticOutcome.Failed,
                result.IsSuccess ? ClientDiagnosticSeverity.Information : ClientDiagnosticSeverity.Warning,
                new Dictionary<string, string> { ["ResultStatus"] = result.Status.ToString() });
            return result;
        }
        catch (Exception exception)
        {
            duration.Stop();
            PublishOperationFailure(
                ClientDiagnosticCategory.ClientCommand,
                "CommandExecutionFailed",
                operationId,
                duration.Elapsed,
                exception,
                target.EndpointId.Value,
                target.AttachmentGeneration.Value,
                target.InstrumentId.Value,
                target.CommandPath.ToString());
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        inner.StatusChanged -= InnerStatusChanged;
        if (inner is IRuntimeHostEventSource eventSource)
        {
            eventSource.EventOccurred -= InnerEventOccurred;
        }

        await inner.DisposeAsync().ConfigureAwait(false);
        Publish(ClientDiagnosticCategory.ClientLifecycle, "SessionStopped");
    }

    private async Task<RemotePropertyOperationResult> RunPropertyOperationAsync(
        string operationName,
        RemotePropertyTarget target,
        Func<Task<RemotePropertyOperationResult>> operation)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Guid operationId = Guid.NewGuid();
        Stopwatch duration = Stopwatch.StartNew();
        PublishPropertyTarget(operationName + "Started", target, operationId, ClientDiagnosticDirection.Outbound);

        try
        {
            RemotePropertyOperationResult result = await operation().ConfigureAwait(false);
            duration.Stop();
            PublishPropertyTarget(
                operationName + "Completed",
                target,
                operationId,
                ClientDiagnosticDirection.Inbound,
                duration.Elapsed,
                result.IsSuccess ? ClientDiagnosticOutcome.Succeeded : ClientDiagnosticOutcome.Failed,
                result.IsSuccess ? ClientDiagnosticSeverity.Information : ClientDiagnosticSeverity.Warning,
                new Dictionary<string, string> { ["ResultStatus"] = result.Status.ToString() });
            return result;
        }
        catch (Exception exception)
        {
            duration.Stop();
            PublishOperationFailure(
                ClientDiagnosticCategory.ClientProperty,
                operationName + "Failed",
                operationId,
                duration.Elapsed,
                exception,
                target.EndpointId.Value,
                target.AttachmentGeneration.Value,
                target.InstrumentId.Value,
                target.PropertyId.Value);
            throw;
        }
    }

    private void InnerStatusChanged(
        object? sender,
        RuntimeHostClientSessionStatusChangedEventArgs eventArgs)
    {
        string eventName = eventArgs.Current.State switch
        {
            RuntimeHostClientSessionState.Connecting => "ConnectionStarted",
            RuntimeHostClientSessionState.Connected => "ConnectionSucceeded",
            RuntimeHostClientSessionState.Reconnecting => "RecoveryScheduled",
            RuntimeHostClientSessionState.Disconnecting => "DisconnectStarted",
            RuntimeHostClientSessionState.Disconnected => "Disconnected",
            RuntimeHostClientSessionState.Faulted => "ConnectionFaulted",
            _ => "ConnectionStateChanged"
        };
        ClientDiagnosticCategory category =
            eventArgs.Current.State == RuntimeHostClientSessionState.Reconnecting
                ? ClientDiagnosticCategory.ClientRecovery
                : ClientDiagnosticCategory.ClientConnection;
        Publish(
            category,
            eventName,
            severity: eventArgs.Current.State == RuntimeHostClientSessionState.Faulted
                ? ClientDiagnosticSeverity.Error
                : ClientDiagnosticSeverity.Information,
            metadata: new Dictionary<string, string>
            {
                ["PreviousState"] = eventArgs.Previous.State.ToString(),
                ["CurrentState"] = eventArgs.Current.State.ToString()
            });
        StatusChanged?.Invoke(this, eventArgs);
    }

    private void InnerEventOccurred(object? sender, RemoteEventOccurredEventArgs eventArgs)
    {
        RemoteRuntimeHostObservation observation = eventArgs.Observation;
        var payload = (RemoteEventOccurredObservationPayload)observation.Payload;
        PublishTarget(
            ClientDiagnosticCategory.ClientObservation,
            "EventDelivered",
            observation.Attachment.EndpointId.Value,
            observation.Attachment.Generation.Value,
            payload.InstrumentId.Value,
            payload.EventPath.ToString(),
            operationId: null,
            direction: ClientDiagnosticDirection.Inbound);
        EventOccurred?.Invoke(this, eventArgs);
    }

    private void PublishPropertyTarget(
        string eventName,
        RemotePropertyTarget target,
        Guid operationId,
        ClientDiagnosticDirection direction,
        TimeSpan? duration = null,
        ClientDiagnosticOutcome? outcome = null,
        ClientDiagnosticSeverity severity = ClientDiagnosticSeverity.Information,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        PublishTarget(
            ClientDiagnosticCategory.ClientProperty,
            eventName,
            target.EndpointId.Value,
            target.AttachmentGeneration.Value,
            target.InstrumentId.Value,
            target.PropertyId.Value,
            operationId,
            direction,
            duration,
            outcome,
            severity,
            metadata);

    private void PublishOperationFailure(
        ClientDiagnosticCategory category,
        string eventName,
        Guid operationId,
        TimeSpan duration,
        Exception exception,
        string endpointId,
        Guid generation,
        string instrumentId,
        string descriptorPath) =>
        PublishTarget(
            category,
            eventName,
            endpointId,
            generation,
            instrumentId,
            descriptorPath,
            operationId,
            direction: null,
            duration: duration,
            outcome: IsCancellation(exception)
                ? ClientDiagnosticOutcome.Cancelled
                : ClientDiagnosticOutcome.Failed,
            severity: IsCancellation(exception)
                ? ClientDiagnosticSeverity.Information
                : ClientDiagnosticSeverity.Error);

    private static bool IsCancellation(Exception exception) =>
        exception is OperationCanceledException ||
        exception is RuntimeHostClientException
        {
            Category: RuntimeHostClientFailureCategory.Cancelled
        };

    private void PublishTarget(
        ClientDiagnosticCategory category,
        string eventName,
        string endpointId,
        Guid generation,
        string instrumentId,
        string descriptorPath,
        Guid? operationId,
        ClientDiagnosticDirection? direction,
        TimeSpan? duration = null,
        ClientDiagnosticOutcome? outcome = null,
        ClientDiagnosticSeverity severity = ClientDiagnosticSeverity.Information,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        diagnostics.Publish(
            ClientDiagnosticLevel.Operational,
            () => new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Operational,
                category,
                eventName,
                severity,
                direction,
                operationId,
                endpointId,
                generation,
                instrumentId,
                descriptorPath,
                duration,
                outcome,
                metadata));

    private void Publish(
        ClientDiagnosticCategory category,
        string eventName,
        ClientDiagnosticSeverity severity = ClientDiagnosticSeverity.Information,
        ClientDiagnosticDirection? direction = null,
        Guid? operationId = null,
        TimeSpan? duration = null,
        ClientDiagnosticOutcome? outcome = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        diagnostics.Publish(
            ClientDiagnosticLevel.Operational,
            () => new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Operational,
                category,
                eventName,
                severity,
                direction,
                operationId,
                duration: duration,
                outcome: outcome,
                metadata: metadata));

}
