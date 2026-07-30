using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Diagnostics;

/// <summary>
/// Converts authoritative runtime endpoint status transitions into
/// structured operational diagnostics.
/// </summary>
internal sealed class RuntimeEndpointLifecycleDiagnosticObserver :
    IEndpointConnectionStatusObserver
{
    private readonly RuntimeEndpoint endpoint;
    private readonly RuntimeDiagnosticPublisher diagnostics;
    private bool recoveryInProgress;

    public RuntimeEndpointLifecycleDiagnosticObserver(
        RuntimeEndpoint endpoint,
        RuntimeDiagnosticPublisher diagnostics)
    {
        this.endpoint =
            endpoint ??
            throw new ArgumentNullException(
                nameof(endpoint));

        this.diagnostics =
            diagnostics ??
            throw new ArgumentNullException(
                nameof(diagnostics));
    }

    public void OnEndpointConnectionStatusChanged(
        EndpointConnectionStatusChanged change)
    {
        ArgumentNullException.ThrowIfNull(
            change);

        PublishStateChanged(
            change);

        switch (change.CurrentStatus.State)
        {
            case EndpointConnectionState.Connecting:
                Publish(
                    RuntimeDiagnosticCategory.RuntimeAttachment,
                    "AttachmentStarted");
                break;

            case EndpointConnectionState.Synchronizing:
                Publish(
                    RuntimeDiagnosticCategory.RuntimeSynchronization,
                    "SynchronizationStarted");
                break;

            case EndpointConnectionState.Reconnecting:
                recoveryInProgress =
                    true;

                Publish(
                    RuntimeDiagnosticCategory.RuntimeRecovery,
                    "RecoveryStarted");
                break;

            case EndpointConnectionState.Ready:
                Publish(
                    RuntimeDiagnosticCategory.RuntimeSynchronization,
                    "SynchronizationCompleted",
                    outcome:
                        RuntimeDiagnosticOutcome.Succeeded);

                Publish(
                    RuntimeDiagnosticCategory.RuntimeAttachment,
                    "AttachmentReady",
                    outcome:
                        RuntimeDiagnosticOutcome.Succeeded);

                if (recoveryInProgress)
                {
                    recoveryInProgress =
                        false;

                    Publish(
                        RuntimeDiagnosticCategory.RuntimeRecovery,
                        "RecoveryCompleted",
                        outcome:
                            RuntimeDiagnosticOutcome.Succeeded);
                }

                break;

            case EndpointConnectionState.Faulted:
                if (recoveryInProgress)
                {
                    recoveryInProgress =
                        false;

                    Publish(
                        RuntimeDiagnosticCategory.RuntimeRecovery,
                        "RecoveryCompleted",
                        RuntimeDiagnosticSeverity.Warning,
                        RuntimeDiagnosticOutcome.Failed);
                }

                break;

            case EndpointConnectionState.Disconnected:
                if (recoveryInProgress)
                {
                    recoveryInProgress =
                        false;

                    Publish(
                        RuntimeDiagnosticCategory.RuntimeRecovery,
                        "RecoveryCompleted",
                        outcome:
                            RuntimeDiagnosticOutcome.Cancelled);
                }

                break;
        }
    }

    private void PublishStateChanged(
        EndpointConnectionStatusChanged change)
    {
        diagnostics.Publish(
            RuntimeDiagnosticLevel.Operational,
            () =>
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    RuntimeDiagnosticCategory.RuntimeConnection,
                    "ConnectionStateChanged",
                    change.CurrentStatus.State
                        == EndpointConnectionState.Faulted
                            ? RuntimeDiagnosticSeverity.Warning
                            : RuntimeDiagnosticSeverity.Information,
                    endpoint.Descriptor.Id.Value,
                    details:
                        new Dictionary<string, string>(
                            StringComparer.Ordinal)
                        {
                            ["PreviousState"] =
                                change.PreviousStatus.State.ToString(),
                            ["CurrentState"] =
                                change.CurrentStatus.State.ToString()
                        }));
    }

    private void Publish(
        RuntimeDiagnosticCategory category,
        string eventName,
        RuntimeDiagnosticSeverity severity =
            RuntimeDiagnosticSeverity.Information,
        RuntimeDiagnosticOutcome? outcome = null)
    {
        diagnostics.Publish(
            RuntimeDiagnosticLevel.Operational,
            () =>
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    category,
                    eventName,
                    severity,
                    endpoint.Descriptor.Id.Value,
                    outcome:
                        outcome));
    }
}
