using Hase.Core.Domain.Data;

namespace Hase.Simulation.Runtime.ByteBuffer;

/// <summary>
/// Owns the authoritative opaque contents of one simulated byte buffer.
/// </summary>
public sealed class ByteBufferSimulation
{
    private ByteArrayValue value =
        new(
            ReadOnlySpan<byte>.Empty);

    /// <summary>
    /// Gets the current immutable buffer value.
    /// </summary>
    public ByteArrayValue Value =>
        value;

    /// <summary>
    /// Replaces the current buffer without interpreting its contents.
    /// </summary>
    public void Replace(
        ByteArrayValue replacement)
    {
        value =
            replacement
            ?? throw new ArgumentNullException(
                nameof(replacement));
    }
}
