namespace Hase.Runtime.Transport;

/// <summary>
/// Provides an immutable snapshot of runtime endpoint connection-supervision
/// statistics.
/// </summary>
public sealed record RuntimeEndpointConnectionStatistics
{
    /// <summary>
    /// Initializes a connection-statistics snapshot.
    /// </summary>
    public RuntimeEndpointConnectionStatistics(
        long initialConnectionAttemptCount,
        long initialConnectionFailureCount,
        long reconnectAttemptCount,
        long reconnectFailureCount,
        long successfulRecoveryCount,
        DateTimeOffset? lastRecoveryStartedAtUtc = null,
        DateTimeOffset? lastRecoveryCompletedAtUtc = null,
        TimeSpan? lastRecoveryDuration = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(
            initialConnectionAttemptCount);

        ArgumentOutOfRangeException.ThrowIfNegative(
            initialConnectionFailureCount);

        ArgumentOutOfRangeException.ThrowIfNegative(
            reconnectAttemptCount);

        ArgumentOutOfRangeException.ThrowIfNegative(
            reconnectFailureCount);

        ArgumentOutOfRangeException.ThrowIfNegative(
            successfulRecoveryCount);

        ValidateUtcTimestamp(
            lastRecoveryStartedAtUtc,
            nameof(lastRecoveryStartedAtUtc));

        ValidateUtcTimestamp(
            lastRecoveryCompletedAtUtc,
            nameof(lastRecoveryCompletedAtUtc));

        if (lastRecoveryDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastRecoveryDuration),
                lastRecoveryDuration,
                "The recovery duration must not be negative.");
        }

        InitialConnectionAttemptCount =
            initialConnectionAttemptCount;

        InitialConnectionFailureCount =
            initialConnectionFailureCount;

        ReconnectAttemptCount =
            reconnectAttemptCount;

        ReconnectFailureCount =
            reconnectFailureCount;

        SuccessfulRecoveryCount =
            successfulRecoveryCount;

        LastRecoveryStartedAtUtc =
            lastRecoveryStartedAtUtc;

        LastRecoveryCompletedAtUtc =
            lastRecoveryCompletedAtUtc;

        LastRecoveryDuration =
            lastRecoveryDuration;
    }

    /// <summary>
    /// Gets an empty statistics snapshot.
    /// </summary>
    public static RuntimeEndpointConnectionStatistics Empty
    {
        get;
    } =
        new(
            initialConnectionAttemptCount:
                0,
            initialConnectionFailureCount:
                0,
            reconnectAttemptCount:
                0,
            reconnectFailureCount:
                0,
            successfulRecoveryCount:
                0);

    /// <summary>
    /// Gets the number of initial transport connection attempts.
    /// </summary>
    public long InitialConnectionAttemptCount
    {
        get;
    }

    /// <summary>
    /// Gets the number of failed initial transport connection attempts.
    /// </summary>
    public long InitialConnectionFailureCount
    {
        get;
    }

    /// <summary>
    /// Gets the number of endpoint recovery attempts.
    /// </summary>
    public long ReconnectAttemptCount
    {
        get;
    }

    /// <summary>
    /// Gets the number of failed endpoint recovery attempts.
    /// </summary>
    public long ReconnectFailureCount
    {
        get;
    }

    /// <summary>
    /// Gets the number of successful endpoint recoveries.
    /// </summary>
    public long SuccessfulRecoveryCount
    {
        get;
    }

    /// <summary>
    /// Gets the UTC start time of the most recently started recovery.
    /// </summary>
    public DateTimeOffset? LastRecoveryStartedAtUtc
    {
        get;
    }

    /// <summary>
    /// Gets the UTC completion time of the most recently completed recovery.
    /// </summary>
    public DateTimeOffset? LastRecoveryCompletedAtUtc
    {
        get;
    }

    /// <summary>
    /// Gets the duration of the most recently completed recovery.
    /// </summary>
    public TimeSpan? LastRecoveryDuration
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