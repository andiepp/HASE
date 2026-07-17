namespace Hase.Transport;

/// <summary>
/// Provides an immutable snapshot of completed transport-exchange statistics.
/// </summary>
/// <remarks>
/// The statistics contain transport metadata only. They do not contain request
/// or response payload bytes and do not interpret protocol messages.
/// </remarks>
public sealed record TransportExchangeStatistics
{
    /// <summary>
    /// Initializes a transport-exchange statistics snapshot.
    /// </summary>
    public TransportExchangeStatistics(
        long completedExchangeCount,
        long successfulExchangeCount,
        long failedExchangeCount,
        long cancelledExchangeCount,
        long totalRequestByteCount,
        long totalResponseByteCount,
        TimeSpan totalDuration,
        DateTimeOffset? lastCompletedAtUtc = null,
        TransportExchangeOutcome? lastOutcome = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(
            completedExchangeCount);

        ArgumentOutOfRangeException.ThrowIfNegative(
            successfulExchangeCount);

        ArgumentOutOfRangeException.ThrowIfNegative(
            failedExchangeCount);

        ArgumentOutOfRangeException.ThrowIfNegative(
            cancelledExchangeCount);

        ArgumentOutOfRangeException.ThrowIfNegative(
            totalRequestByteCount);

        ArgumentOutOfRangeException.ThrowIfNegative(
            totalResponseByteCount);

        if (totalDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalDuration),
                totalDuration,
                "The total exchange duration must not be negative.");
        }

        long outcomeCount;

        try
        {
            outcomeCount =
                checked(
                    successfulExchangeCount
                    + failedExchangeCount
                    + cancelledExchangeCount);
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException(
                "The exchange outcome counts exceed the supported range.",
                nameof(successfulExchangeCount),
                exception);
        }

        if (completedExchangeCount != outcomeCount)
        {
            throw new ArgumentException(
                "The completed exchange count must equal the sum of the "
                + "successful, failed, and cancelled exchange counts.",
                nameof(completedExchangeCount));
        }

        ValidateUtcTimestamp(
            lastCompletedAtUtc,
            nameof(lastCompletedAtUtc));

        if (lastOutcome.HasValue
            && !Enum.IsDefined(
                lastOutcome.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastOutcome),
                lastOutcome,
                "The last transport exchange outcome is not defined.");
        }

        if (completedExchangeCount == 0)
        {
            if (lastCompletedAtUtc.HasValue
                || lastOutcome.HasValue)
            {
                throw new ArgumentException(
                    "Empty exchange statistics must not contain last-exchange "
                    + "information.",
                    nameof(lastCompletedAtUtc));
            }
        }
        else if (!lastCompletedAtUtc.HasValue
            || !lastOutcome.HasValue)
        {
            throw new ArgumentException(
                "Non-empty exchange statistics must contain complete "
                + "last-exchange information.",
                nameof(lastCompletedAtUtc));
        }

        CompletedExchangeCount =
            completedExchangeCount;

        SuccessfulExchangeCount =
            successfulExchangeCount;

        FailedExchangeCount =
            failedExchangeCount;

        CancelledExchangeCount =
            cancelledExchangeCount;

        TotalRequestByteCount =
            totalRequestByteCount;

        TotalResponseByteCount =
            totalResponseByteCount;

        TotalDuration =
            totalDuration;

        LastCompletedAtUtc =
            lastCompletedAtUtc;

        LastOutcome =
            lastOutcome;
    }

    /// <summary>
    /// Gets an empty statistics snapshot.
    /// </summary>
    public static TransportExchangeStatistics Empty
    {
        get;
    } =
        new(
            completedExchangeCount:
                0,
            successfulExchangeCount:
                0,
            failedExchangeCount:
                0,
            cancelledExchangeCount:
                0,
            totalRequestByteCount:
                0,
            totalResponseByteCount:
                0,
            totalDuration:
                TimeSpan.Zero);

    /// <summary>
    /// Gets the total number of completed exchanges.
    /// </summary>
    public long CompletedExchangeCount
    {
        get;
    }

    /// <summary>
    /// Gets the number of successful exchanges.
    /// </summary>
    public long SuccessfulExchangeCount
    {
        get;
    }

    /// <summary>
    /// Gets the number of failed exchanges.
    /// </summary>
    public long FailedExchangeCount
    {
        get;
    }

    /// <summary>
    /// Gets the number of cancelled exchanges.
    /// </summary>
    public long CancelledExchangeCount
    {
        get;
    }

    /// <summary>
    /// Gets the total number of request bytes reported by completed exchanges.
    /// </summary>
    public long TotalRequestByteCount
    {
        get;
    }

    /// <summary>
    /// Gets the total number of response bytes reported by completed exchanges.
    /// </summary>
    public long TotalResponseByteCount
    {
        get;
    }

    /// <summary>
    /// Gets the cumulative monotonic duration of completed exchanges.
    /// </summary>
    public TimeSpan TotalDuration
    {
        get;
    }

    /// <summary>
    /// Gets the UTC completion time of the most recently observed exchange.
    /// </summary>
    public DateTimeOffset? LastCompletedAtUtc
    {
        get;
    }

    /// <summary>
    /// Gets the outcome of the most recently observed exchange.
    /// </summary>
    public TransportExchangeOutcome? LastOutcome
    {
        get;
    }

    private static void ValidateUtcTimestamp(
        DateTimeOffset? timestamp,
        string parameterName)
    {
        if (timestamp.HasValue
            && timestamp.Value.Offset
                != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The timestamp must be expressed in UTC.",
                parameterName);
        }
    }
}