namespace Hase.Runtime.Northbound;

/// <summary>
/// Identifies one published runtime endpoint attachment lifetime.
/// </summary>
/// <remarks>
/// A generation is local to one runtime host and changes whenever an
/// attachment ends and another attachment is published. It is opaque to
/// applications and is not endpoint identity, a transport address, a
/// descriptor version, or a protocol correlation identifier.
/// </remarks>
public sealed record RuntimeEndpointAttachmentGeneration
{
    /// <summary>
    /// Initializes an attachment generation from a non-empty opaque value.
    /// </summary>
    public RuntimeEndpointAttachmentGeneration(
        Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "An attachment generation value must not be empty.",
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

    /// <summary>
    /// Creates a new attachment generation.
    /// </summary>
    public static RuntimeEndpointAttachmentGeneration CreateNew()
    {
        return new RuntimeEndpointAttachmentGeneration(
            Guid.NewGuid());
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString(
            "D");
    }
}