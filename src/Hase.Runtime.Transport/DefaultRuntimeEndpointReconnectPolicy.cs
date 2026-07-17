namespace Hase.Runtime.Transport;

/// <summary>
/// Provides the default runtime endpoint reconnect schedule.
/// </summary>
/// <remarks>
/// The first reconnect attempt is immediate. Subsequent attempts wait
/// one second, two seconds, five seconds, and then ten seconds.
/// The ten-second delay is the maximum and is used for all later attempts.
/// </remarks>
public sealed class DefaultRuntimeEndpointReconnectPolicy
    : IRuntimeEndpointReconnectPolicy
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5)
    ];

    private static readonly TimeSpan MaximumRetryDelay =
        TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public TimeSpan GetDelay(
        int retryAttempt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(
            retryAttempt);

        if (retryAttempt < RetryDelays.Length)
        {
            return RetryDelays[retryAttempt];
        }

        return MaximumRetryDelay;
    }
}
