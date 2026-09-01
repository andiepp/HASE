using Hase.DesktopHost.Hosting;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.App.Hosting;

internal sealed class DesktopRuntimeHostEndpointStartupCoordinator
{
    private readonly RuntimeDiagnosticPublisher diagnostics;

    public DesktopRuntimeHostEndpointStartupCoordinator(
        RuntimeDiagnosticPublisher diagnostics)
    {
        this.diagnostics =
            diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public async Task<bool> TryAttachAsync(
        string endpointId,
        string endpointKind,
        Func<CancellationToken, Task> attachAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointKind);
        ArgumentNullException.ThrowIfNull(attachAsync);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await attachAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (DesktopRuntimeHostEndpointFailureClassification
                .TryClassifyUnavailableFailure(
                exception,
                out string failureCategory))
        {
            PublishUnavailable(
                endpointId,
                endpointKind,
                failureCategory);
            return false;
        }
    }

    private void PublishUnavailable(
        string endpointId,
        string endpointKind,
        string failureCategory)
    {
        diagnostics.Publish(
            RuntimeDiagnosticLevel.Operational,
            () =>
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    RuntimeDiagnosticCategory.RuntimeAttachment,
                    "EndpointStartupUnavailable",
                    RuntimeDiagnosticSeverity.Warning,
                    endpointId,
                    outcome: RuntimeDiagnosticOutcome.Failed,
                    details:
                        new Dictionary<string, string>(
                            StringComparer.Ordinal)
                        {
                            ["EndpointKind"] = endpointKind,
                            ["FailureCategory"] = failureCategory
                        }));
    }
}
