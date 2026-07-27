using Hase.Core.Domain.Identity;

namespace Hase.Client;

/// <summary>
/// Identifies exactly one published endpoint attachment in the normalized
/// client model.
/// </summary>
public sealed record RemoteEndpointAttachmentKey
{
    /// <summary>
    /// Initializes one generation-scoped endpoint attachment key.
    /// </summary>
    public RemoteEndpointAttachmentKey(
        EndpointId endpointId,
        RemoteEndpointAttachmentGeneration generation)
    {
        EndpointId =
            endpointId
            ?? throw new ArgumentNullException(
                nameof(endpointId));

        Generation =
            generation
            ?? throw new ArgumentNullException(
                nameof(generation));
    }

    /// <summary>
    /// Gets the authoritative endpoint identity.
    /// </summary>
    public EndpointId EndpointId
    {
        get;
    }

    /// <summary>
    /// Gets the opaque published attachment generation.
    /// </summary>
    public RemoteEndpointAttachmentGeneration Generation
    {
        get;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{EndpointId}@{Generation}";
    }
}
