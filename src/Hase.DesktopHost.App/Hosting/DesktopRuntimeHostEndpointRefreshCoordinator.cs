using System.Diagnostics;
using System.Globalization;
using System.IO;
using Hase.DesktopHost.Hosting;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.App.Hosting;

internal sealed class DesktopRuntimeHostEndpointRefreshTarget
{
    public DesktopRuntimeHostEndpointRefreshTarget(
        string endpointId,
        string endpointKind,
        Func<CancellationToken, Task> attachAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointKind);
        ArgumentNullException.ThrowIfNull(attachAsync);

        EndpointId = endpointId.Trim();
        EndpointKind = endpointKind.Trim();
        AttachAsync = attachAsync;
    }

    public string EndpointId { get; }

    public string EndpointKind { get; }

    public Func<CancellationToken, Task> AttachAsync { get; }
}

internal sealed class DesktopRuntimeHostEndpointRefreshCoordinator
{
    private readonly Func<string, bool> isPublished;
    private readonly RuntimeDiagnosticPublisher diagnostics;
    private readonly SemaphoreSlim operationGate = new(1, 1);

    public DesktopRuntimeHostEndpointRefreshCoordinator(
        Func<string, bool> isPublished,
        RuntimeDiagnosticPublisher diagnostics)
    {
        this.isPublished =
            isPublished
            ?? throw new ArgumentNullException(nameof(isPublished));
        this.diagnostics =
            diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public async Task RefreshAsync(
        IReadOnlyList<DesktopRuntimeHostEndpointRefreshTarget> targets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targets);
        cancellationToken.ThrowIfCancellationRequested();

        await operationGate.WaitAsync(cancellationToken);

        Guid operationId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        int attachedCount = 0;
        int unavailableCount = 0;
        int failedCount = 0;
        int skippedCount = 0;

        try
        {
            PublishStarted(
                operationId,
                targets.Count);

            foreach (DesktopRuntimeHostEndpointRefreshTarget target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (isPublished(target.EndpointId))
                {
                    skippedCount++;
                    PublishSkipped(
                        operationId,
                        target);
                    continue;
                }

                try
                {
                    await target.AttachAsync(cancellationToken);

                    if (!isPublished(target.EndpointId))
                    {
                        throw new InvalidDataException(
                            "The configured endpoint attachment did not publish "
                            + "the expected authoritative identity.");
                    }

                    attachedCount++;
                    PublishAttached(
                        operationId,
                        target);
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
                    unavailableCount++;
                    PublishUnavailable(
                        operationId,
                        target,
                        failureCategory);
                }
                catch (Exception exception)
                {
                    failedCount++;
                    PublishFailed(
                        operationId,
                        target,
                        ClassifyFailure(exception));
                }
            }

            stopwatch.Stop();
            PublishCompleted(
                operationId,
                stopwatch.Elapsed,
                targets.Count,
                attachedCount,
                unavailableCount,
                failedCount,
                skippedCount);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            PublishCancelled(
                operationId,
                stopwatch.Elapsed,
                targets.Count,
                attachedCount,
                unavailableCount,
                failedCount,
                skippedCount);
            throw;
        }
        finally
        {
            operationGate.Release();
        }
    }

    private void PublishStarted(
        Guid operationId,
        int targetCount)
    {
        diagnostics.Publish(
            RuntimeDiagnosticLevel.Operational,
            () =>
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    RuntimeDiagnosticCategory.RuntimeAttachment,
                    "EndpointRefreshStarted",
                    operationId: operationId,
                    details: CountDetails(
                        ("TargetCount", targetCount))));
    }

    private void PublishSkipped(
        Guid operationId,
        DesktopRuntimeHostEndpointRefreshTarget target)
    {
        diagnostics.Publish(
            RuntimeDiagnosticLevel.Operational,
            () =>
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    RuntimeDiagnosticCategory.RuntimeAttachment,
                    "EndpointRefreshSkippedPublished",
                    endpointId: target.EndpointId,
                    operationId: operationId,
                    outcome: RuntimeDiagnosticOutcome.Succeeded,
                    details: EndpointDetails(target)));
    }

    private void PublishAttached(
        Guid operationId,
        DesktopRuntimeHostEndpointRefreshTarget target)
    {
        diagnostics.Publish(
            RuntimeDiagnosticLevel.Operational,
            () =>
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    RuntimeDiagnosticCategory.RuntimeAttachment,
                    "EndpointRefreshAttached",
                    endpointId: target.EndpointId,
                    operationId: operationId,
                    outcome: RuntimeDiagnosticOutcome.Succeeded,
                    details: EndpointDetails(target)));
    }

    private void PublishUnavailable(
        Guid operationId,
        DesktopRuntimeHostEndpointRefreshTarget target,
        string failureCategory)
    {
        diagnostics.Publish(
            RuntimeDiagnosticLevel.Operational,
            () =>
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    RuntimeDiagnosticCategory.RuntimeAttachment,
                    "EndpointRefreshUnavailable",
                    RuntimeDiagnosticSeverity.Warning,
                    target.EndpointId,
                    operationId: operationId,
                    outcome: RuntimeDiagnosticOutcome.Failed,
                    details: FailureDetails(
                        target,
                        failureCategory)));
    }

    private void PublishFailed(
        Guid operationId,
        DesktopRuntimeHostEndpointRefreshTarget target,
        string failureCategory)
    {
        diagnostics.Publish(
            RuntimeDiagnosticLevel.Operational,
            () =>
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    RuntimeDiagnosticCategory.RuntimeAttachment,
                    "EndpointRefreshFailed",
                    RuntimeDiagnosticSeverity.Error,
                    target.EndpointId,
                    operationId: operationId,
                    outcome: RuntimeDiagnosticOutcome.Failed,
                    details: FailureDetails(
                        target,
                        failureCategory)));
    }

    private void PublishCompleted(
        Guid operationId,
        TimeSpan duration,
        int targetCount,
        int attachedCount,
        int unavailableCount,
        int failedCount,
        int skippedCount)
    {
        diagnostics.Publish(
            RuntimeDiagnosticLevel.Operational,
            () =>
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    RuntimeDiagnosticCategory.RuntimeAttachment,
                    "EndpointRefreshCompleted",
                    failedCount == 0
                        ? RuntimeDiagnosticSeverity.Information
                        : RuntimeDiagnosticSeverity.Warning,
                    operationId: operationId,
                    duration: duration,
                    outcome: failedCount == 0
                        ? RuntimeDiagnosticOutcome.Succeeded
                        : RuntimeDiagnosticOutcome.Failed,
                    details: SummaryDetails(
                        targetCount,
                        attachedCount,
                        unavailableCount,
                        failedCount,
                        skippedCount)));
    }

    private void PublishCancelled(
        Guid operationId,
        TimeSpan duration,
        int targetCount,
        int attachedCount,
        int unavailableCount,
        int failedCount,
        int skippedCount)
    {
        diagnostics.Publish(
            RuntimeDiagnosticLevel.Operational,
            () =>
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    RuntimeDiagnosticCategory.RuntimeAttachment,
                    "EndpointRefreshCancelled",
                    RuntimeDiagnosticSeverity.Warning,
                    operationId: operationId,
                    duration: duration,
                    outcome: RuntimeDiagnosticOutcome.Cancelled,
                    details: SummaryDetails(
                        targetCount,
                        attachedCount,
                        unavailableCount,
                        failedCount,
                        skippedCount)));
    }

    private static IReadOnlyDictionary<string, string> EndpointDetails(
        DesktopRuntimeHostEndpointRefreshTarget target) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["EndpointKind"] = target.EndpointKind
        };

    private static IReadOnlyDictionary<string, string> FailureDetails(
        DesktopRuntimeHostEndpointRefreshTarget target,
        string failureCategory) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["EndpointKind"] = target.EndpointKind,
            ["FailureCategory"] = failureCategory
        };

    private static IReadOnlyDictionary<string, string> SummaryDetails(
        int targetCount,
        int attachedCount,
        int unavailableCount,
        int failedCount,
        int skippedCount) =>
        CountDetails(
            ("TargetCount", targetCount),
            ("AttachedCount", attachedCount),
            ("UnavailableCount", unavailableCount),
            ("FailedCount", failedCount),
            ("SkippedCount", skippedCount));

    private static IReadOnlyDictionary<string, string> CountDetails(
        params (string Name, int Value)[] values) =>
        values.ToDictionary(
            item => item.Name,
            item => item.Value.ToString(CultureInfo.InvariantCulture),
            StringComparer.Ordinal);

    private static string ClassifyFailure(Exception exception) =>
        exception switch
        {
            InvalidDataException => "AuthoritativeIdentityRejected",
            InvalidOperationException => "AmbiguousOrDuplicateCandidate",
            NotSupportedException => "UnsupportedEndpoint",
            ArgumentException => "InvalidConfiguration",
            _ => "UnexpectedFailure"
        };
}
