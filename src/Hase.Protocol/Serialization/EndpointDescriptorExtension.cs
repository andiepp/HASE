namespace Hase.Protocol.Serialization;

/// <summary>
/// Represents one length-delimited endpoint descriptor extension.
/// </summary>
internal sealed class EndpointDescriptorExtension
{
    private readonly byte[] _payload;

    public EndpointDescriptorExtension(
        byte type,
        ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            throw new ArgumentException(
                "An endpoint descriptor extension payload must not be empty.",
                nameof(payload));
        }

        Type = type;
        _payload = payload.ToArray();
    }

    /// <summary>
    /// Gets the stable wire identifier for the extension type.
    /// </summary>
    public byte Type { get; }

    /// <summary>
    /// Gets a read-only view of the extension payload.
    /// </summary>
    public ReadOnlySpan<byte> Payload =>
        _payload;
}
