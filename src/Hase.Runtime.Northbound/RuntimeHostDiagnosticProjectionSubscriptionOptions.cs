namespace Hase.Runtime.Northbound;

/// <summary>
/// Defines bounded delivery options for one live-only diagnostic projection
/// subscription.
/// </summary>
public sealed record RuntimeHostDiagnosticProjectionSubscriptionOptions
{
    public const int DefaultBufferCapacity = 256;

    public RuntimeHostDiagnosticProjectionSubscriptionOptions(
        int bufferCapacity = DefaultBufferCapacity)
    {
        if (bufferCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bufferCapacity),
                bufferCapacity,
                "The projection buffer capacity must be greater than zero.");
        }

        BufferCapacity = bufferCapacity;
    }

    public int BufferCapacity { get; }
}
