namespace Hase.Transport;

/// <summary>
/// Collects aggregate statistics from completed transport-exchange traces.
/// </summary>
/// <remarks>
/// The collector is thread-safe and may be subscribed to any transport
/// connection that implements <see cref="ITransportExchangeTraceSource"/>.
/// </remarks>
public sealed class TransportExchangeStatisticsCollector
    : ITransportExchangeTraceObserver
{
    private readonly object _syncRoot =
        new();

    private long _completedExchangeCount;

    private long _successfulExchangeCount;

    private long _failedExchangeCount;

    private long _cancelledExchangeCount;

    private long _totalRequestByteCount;

    private long _totalResponseByteCount;

    private TimeSpan _totalDuration;

    private DateTimeOffset? _lastCompletedAtUtc;

    private TransportExchangeOutcome? _lastOutcome;

    /// <summary>
    /// Creates an immutable snapshot of the currently collected statistics.
    /// </summary>
    public TransportExchangeStatistics GetStatistics()
    {
        lock (_syncRoot)
        {
            return new TransportExchangeStatistics(
                completedExchangeCount:
                    _completedExchangeCount,
                successfulExchangeCount:
                    _successfulExchangeCount,
                failedExchangeCount:
                    _failedExchangeCount,
                cancelledExchangeCount:
                    _cancelledExchangeCount,
                totalRequestByteCount:
                    _totalRequestByteCount,
                totalResponseByteCount:
                    _totalResponseByteCount,
                totalDuration:
                    _totalDuration,
                lastCompletedAtUtc:
                    _lastCompletedAtUtc,
                lastOutcome:
                    _lastOutcome);
        }
    }

    /// <inheritdoc />
    public void OnTransportExchangeCompleted(
        TransportExchangeTrace trace)
    {
        ArgumentNullException.ThrowIfNull(
            trace);

        lock (_syncRoot)
        {
            _completedExchangeCount =
                checked(
                    _completedExchangeCount
                    + 1);

            switch (trace.Outcome)
            {
                case TransportExchangeOutcome.Succeeded:
                    _successfulExchangeCount =
                        checked(
                            _successfulExchangeCount
                            + 1);

                    break;

                case TransportExchangeOutcome.Failed:
                    _failedExchangeCount =
                        checked(
                            _failedExchangeCount
                            + 1);

                    break;

                case TransportExchangeOutcome.Cancelled:
                    _cancelledExchangeCount =
                        checked(
                            _cancelledExchangeCount
                            + 1);

                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(trace),
                        trace.Outcome,
                        "The transport exchange outcome is not defined.");
            }

            _totalRequestByteCount =
                checked(
                    _totalRequestByteCount
                    + trace.RequestByteCount);

            _totalResponseByteCount =
                checked(
                    _totalResponseByteCount
                    + trace.ResponseByteCount);

            _totalDuration =
                checked(
                    _totalDuration
                    + trace.Duration);

            _lastCompletedAtUtc =
                trace.CompletedAtUtc;

            _lastOutcome =
                trace.Outcome;
        }
    }
}