namespace Hase.Client;

/// <summary>
/// Identifies one published remote endpoint attachment lifetime.
/// </summary>
/// <remarks>
/// The generation is opaque to clients. It is not endpoint identity, a
/// transport address, a descriptor version, or a protocol correlation value.
/// </remarks>
public sealed record RemoteEndpointAttachmentGeneration
{
    /// <summary>
    /// Initializes one non-empty remote attachment generation.
    /// </summary>
    public RemoteEndpointAttachmentGeneration(
        Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "A remote attachment generation must not be empty.",
                nameof(value));
        }

        Value =
            value;
    }

    /// <summary>
    /// Gets the opaque attachment-generation value.
    /// </summary>
    public Guid Value
    {
        get;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString(
            "D");
    }
}
