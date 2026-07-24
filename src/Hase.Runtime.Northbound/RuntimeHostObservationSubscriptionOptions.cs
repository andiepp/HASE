namespace Hase.Runtime.Northbound;

/// <summary>
/// Defines validated options for one runtime-host observation subscription.
/// </summary>
public sealed record RuntimeHostObservationSubscriptionOptions
{
    /// <summary>
    /// The default maximum number of observations waiting for one subscriber.
    /// </summary>
    public const int DefaultBufferCapacity =
        256;

    /// <summary>
    /// Initializes subscription options.
    /// </summary>
    public RuntimeHostObservationSubscriptionOptions(
        int bufferCapacity = DefaultBufferCapacity)
    {
        if (bufferCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bufferCapacity),
                bufferCapacity,
                "The observation buffer capacity must be greater than zero.");
        }

        BufferCapacity =
            bufferCapacity;
    }

    /// <summary>
    /// Gets the maximum number of observations that may wait for this
    /// subscriber before the subscription enters a terminal observation-gap
    /// state.
    /// </summary>
    public int BufferCapacity
    {
        get;
    }
}