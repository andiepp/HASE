using System.IO;
using System.Net.Sockets;
using Hase.Runtime.Diagnostics;
using Hase.Transport.Serial;

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
            when (TryClassifyUnavailableFailure(
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

    internal static bool TryClassifyUnavailableFailure(
        Exception exception,
        out string failureCategory)
    {
        if (exception is DesktopRuntimeHostEndpointUnavailableException
            unavailable)
        {
            failureCategory = unavailable.FailureCategory;
            return true;
        }

        if (exception is AggregateException aggregate)
        {
            Exception[] failures = aggregate.Flatten().InnerExceptions.ToArray();
            if (failures.Length > 0
                && failures.All(
                    failure =>
                        TryClassifyUnavailableFailure(
                            failure,
                            out _)))
            {
                failureCategory = "MultipleAvailabilityFailures";
                return true;
            }
        }

        failureCategory = exception switch
        {
            SerialPortOpenException serialPortFailure =>
                serialPortFailure.Failure switch
                {
                    SerialPortOpenFailure.Busy => "SerialPortBusy",
                    SerialPortOpenFailure.Unavailable =>
                        "SerialPortUnavailable",
                    SerialPortOpenFailure.AccessDenied =>
                        "SerialPortAccessDenied",
                    SerialPortOpenFailure.Failed =>
                        "SerialPortOpenFailed",
                    _ => string.Empty
                },
            TimeoutException => "TimedOut",
            OperationCanceledException => "TimedOut",
            SocketException => "NetworkUnavailable",
            UnauthorizedAccessException => "AccessUnavailable",
            IOException when exception is not InvalidDataException =>
                "IoUnavailable",
            _ => string.Empty
        };

        return failureCategory.Length > 0;
    }
}

internal sealed class DesktopRuntimeHostEndpointUnavailableException
    : Exception
{
    public DesktopRuntimeHostEndpointUnavailableException(
        string failureCategory)
        : base("The configured endpoint is unavailable.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCategory);
        FailureCategory = failureCategory;
    }

    public string FailureCategory { get; }
}
