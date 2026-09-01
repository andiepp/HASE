using System.IO;
using System.Net.Sockets;
using Hase.Transport.Serial;

namespace Hase.DesktopHost.Hosting;

/// <summary>
/// Reports that a configured endpoint is not reachable right now, without
/// disclosing the target it was reached through.
/// </summary>
/// <remarks>
/// A provider raises this when its own attachment establishes that the
/// endpoint is absent or busy rather than misconfigured. The host treats it as
/// an availability outcome and continues with the remaining endpoints.
/// </remarks>
public sealed class DesktopRuntimeHostEndpointUnavailableException
    : Exception
{
    public DesktopRuntimeHostEndpointUnavailableException(
        string failureCategory)
        : base("The configured endpoint is unavailable.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCategory);
        FailureCategory = failureCategory;
    }

    /// <summary>
    /// Gets the reported availability category.
    /// </summary>
    public string FailureCategory { get; }
}

/// <summary>
/// Separates an endpoint that is merely unavailable from one that is
/// misconfigured or broken.
/// </summary>
/// <remarks>
/// This is host machinery rather than device knowledge, so it sits in the
/// host library where every endpoint provider can reach it. Only failures
/// that classify here let a start continue without the endpoint.
/// </remarks>
public static class DesktopRuntimeHostEndpointFailureClassification
{
    /// <summary>
    /// Classifies one attachment failure as an availability outcome.
    /// </summary>
    public static bool TryClassifyUnavailableFailure(
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
