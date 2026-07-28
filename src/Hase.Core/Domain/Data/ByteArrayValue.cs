namespace Hase.Core.Domain.Data;

/// <summary>
/// Represents an immutable opaque ordered sequence of bytes.
/// </summary>
public sealed class ByteArrayValue
    : IEquatable<ByteArrayValue>
{
    private readonly byte[] _bytes;

    public ByteArrayValue(
        byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        _bytes = bytes.ToArray();
    }

    public ByteArrayValue(
        ReadOnlySpan<byte> bytes)
    {
        _bytes = bytes.ToArray();
    }

    /// <summary>
    /// Gets the number of bytes in the value.
    /// </summary>
    public int Length =>
        _bytes.Length;

    /// <summary>
    /// Gets the byte at the specified zero-based index.
    /// </summary>
    public byte this[int index] =>
        _bytes[index];

    /// <summary>
    /// Returns a read-only view of the bytes.
    /// </summary>
    public ReadOnlySpan<byte> AsSpan()
    {
        return _bytes;
    }

    /// <summary>
    /// Returns a mutable copy of the bytes.
    /// </summary>
    public byte[] ToArray()
    {
        return _bytes.ToArray();
    }

    public bool Equals(
        ByteArrayValue? other)
    {
        return
            other is not null &&
            _bytes.AsSpan().SequenceEqual(
                other._bytes);
    }

    public override bool Equals(
        object? obj)
    {
        return
            obj is ByteArrayValue other &&
            Equals(other);
    }

    public override int GetHashCode()
    {
        HashCode hashCode = new();

        foreach (byte value in _bytes)
        {
            hashCode.Add(value);
        }

        return hashCode.ToHashCode();
    }

    public static bool operator ==(
        ByteArrayValue? left,
        ByteArrayValue? right)
    {
        return EqualityComparer<ByteArrayValue>.Default.Equals(
            left,
            right);
    }

    public static bool operator !=(
        ByteArrayValue? left,
        ByteArrayValue? right)
    {
        return !(left == right);
    }
}
