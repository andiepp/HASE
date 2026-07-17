namespace Hase.Runtime.Transport;

/// <summary>
/// Defines the delay applied before each runtime endpoint reconnect attempt.
/// </summary>
public interface IRuntimeEndpointReconnectPolicy
{
    /// <summary>
    /// Gets the delay before the specified reconnect attempt.
    /// </summary>
    /// <param name="retryAttempt">
    /// Zero-based reconnect attempt number.
    /// A value of zero represents the first reconnect attempt.
    /// </param>
    /// <returns>
    /// The delay to apply before starting the reconnect attempt.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="retryAttempt"/> is negative.
    /// </exception>
    TimeSpan GetDelay(
        int retryAttempt);
}