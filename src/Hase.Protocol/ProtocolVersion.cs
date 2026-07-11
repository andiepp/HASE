namespace Hase.Protocol;

/// <summary>
/// Identifies a version of the HASE protocol.
/// </summary>
public readonly record struct ProtocolVersion(byte Major, byte Minor)
{
    /// <summary>
    /// Gets the protocol version implemented by this library.
    /// </summary>
    public static ProtocolVersion Current { get; } = new(1, 0);

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Major}.{Minor}";
    }
}