namespace Hase.Transport;

/// <summary>
/// Describes one completed transport request/response exchange.
/// </summary>
/// <remarks>
/// The trace contains transport metadata only. It does not contain request or
/// response payload bytes and does not interpret protocol messages.
/// </remarks>
public sealed record TransportExchangeTrace
{
    /// <summary>
    /// Initializes a completed transport exchange trace.
    /// </summary>
    public TransportExchangeTrace(
        long sequenceNumber,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        TimeSpan duration,
        int requestByteCount,
        int responseByteCount,
        TransportExchangeOutcome outcome,
        TransportConnectionState connectionState,
        string? exceptionType = null,
        string? exceptionMessage = null)
    {
        if (sequenceNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber),
                sequenceNumber,
                "The exchange sequence number must be at least one.");
        }

        ValidateUtcTimestamp(
            startedAtUtc,
            nameof(startedAtUtc));

        ValidateUtcTimestamp(
            completedAtUtc,
            nameof(completedAtUtc));

        if (completedAtUtc < startedAtUtc)
        {
            throw new ArgumentException(
                "The completion timestamp must not precede the start "
                + "timestamp.",
                nameof(completedAtUtc));
        }

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "The exchange duration must not be negative.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(
            requestByteCount);

        ArgumentOutOfRangeException.ThrowIfNegative(
            responseByteCount);

        if (!Enum.IsDefined(
                outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "The transport exchange outcome is not defined.");
        }

        if (!Enum.IsDefined(
                connectionState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(connectionState),
                connectionState,
                "The transport connection state is not defined.");
        }

        string? normalizedExceptionType =
            NormalizeOptionalText(
                exceptionType);

        string? normalizedExceptionMessage =
            NormalizeOptionalText(
                exceptionMessage);

        if (outcome == TransportExchangeOutcome.Succeeded)
        {
            if (normalizedExceptionType is not null
                || normalizedExceptionMessage is not null)
            {
                throw new ArgumentException(
                    "A successful exchange must not contain exception "
                    + "information.",
                    nameof(exceptionType));
            }
        }
        else if (normalizedExceptionType is null)
        {
            throw new ArgumentException(
                "A failed or cancelled exchange must contain an "
                + "exception type.",
                nameof(exceptionType));
        }

        SequenceNumber =
            sequenceNumber;

        StartedAtUtc =
            startedAtUtc;

        CompletedAtUtc =
            completedAtUtc;

        Duration =
            duration;

        RequestByteCount =
            requestByteCount;

        ResponseByteCount =
            responseByteCount;

        Outcome =
            outcome;

        ConnectionState =
            connectionState;

        ExceptionType =
            normalizedExceptionType;

        ExceptionMessage =
            normalizedExceptionMessage;
    }

    public long SequenceNumber
    {
        get;
    }

    public DateTimeOffset StartedAtUtc
    {
        get;
    }

    public DateTimeOffset CompletedAtUtc
    {
        get;
    }

    public TimeSpan Duration
    {
        get;
    }

    public int RequestByteCount
    {
        get;
    }

    public int ResponseByteCount
    {
        get;
    }

    public TransportExchangeOutcome Outcome
    {
        get;
    }

    public TransportConnectionState ConnectionState
    {
        get;
    }

    public string? ExceptionType
    {
        get;
    }

    public string? ExceptionMessage
    {
        get;
    }

    private static void ValidateUtcTimestamp(
        DateTimeOffset timestamp,
        string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The timestamp must be expressed in UTC.",
                parameterName);
        }
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
                value)
            ? null
            : value.Trim();
    }
}