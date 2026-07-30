using System.Globalization;
using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Transport;

/// <summary>
/// Adds structured recovery-scheduling diagnostics to an existing reconnect
/// policy without changing its delay decisions.
/// </summary>
public sealed class RuntimeEndpointReconnectDiagnosticPolicy :
    IRuntimeEndpointReconnectPolicy
{
    private readonly IRuntimeEndpointReconnectPolicy innerPolicy;
    private readonly RuntimeDiagnosticPublisher diagnostics;
    private readonly string endpointId;
    private readonly Guid? attachmentGeneration;

    public RuntimeEndpointReconnectDiagnosticPolicy(
        IRuntimeEndpointReconnectPolicy innerPolicy,
        RuntimeDiagnosticPublisher diagnostics,
        string endpointId,
        Guid? attachmentGeneration = null)
    {
        this.innerPolicy =
            innerPolicy ??
            throw new ArgumentNullException(
                nameof(innerPolicy));

        this.diagnostics =
            diagnostics ??
            throw new ArgumentNullException(
                nameof(diagnostics));

        if (string.IsNullOrWhiteSpace(
                endpointId))
        {
            throw new ArgumentException(
                "Endpoint identity must not be empty.",
                nameof(endpointId));
        }

        this.endpointId =
            endpointId.Trim();

        this.attachmentGeneration =
            attachmentGeneration;
    }

    public IRuntimeEndpointReconnectPolicy InnerPolicy =>
        innerPolicy;

    public string EndpointId =>
        endpointId;

    public Guid? AttachmentGeneration =>
        attachmentGeneration;

    /// <inheritdoc />
    public TimeSpan GetDelay(
        int retryAttempt)
    {
        TimeSpan delay =
            innerPolicy.GetDelay(
                retryAttempt);

        diagnostics.Publish(
            RuntimeDiagnosticLevel.Operational,
            () =>
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    RuntimeDiagnosticCategory.RuntimeRecovery,
                    "RecoveryScheduled",
                    endpointId:
                        endpointId,
                    attachmentGeneration:
                        attachmentGeneration,
                    details:
                        new Dictionary<string, string>(
                            StringComparer.Ordinal)
                        {
                            ["AttemptNumber"] =
                                ((long)retryAttempt + 1L)
                                    .ToString(
                                        CultureInfo.InvariantCulture),
                            ["RetryIndex"] =
                                retryAttempt.ToString(
                                    CultureInfo.InvariantCulture),
                            ["DelayMilliseconds"] =
                                delay.TotalMilliseconds.ToString(
                                    "0",
                                    CultureInfo.InvariantCulture)
                        }));

        return delay;
    }
}
