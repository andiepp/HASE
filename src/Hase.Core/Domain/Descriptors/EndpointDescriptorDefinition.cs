using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;

namespace Hase.Core.Domain.Descriptors;

/// <summary>
/// Contains reusable endpoint descriptor content that is independent of a
/// physical endpoint identity.
/// </summary>
public sealed record EndpointDescriptorDefinition
{
    public EndpointDescriptorDefinition()
        : this(
            new EndpointMetadata(),
            [])
    {
    }

    public EndpointDescriptorDefinition(
        EndpointMetadata metadata,
        IEnumerable<InstrumentDescriptor> instruments)
    {
        Metadata =
            metadata
            ?? throw new ArgumentNullException(
                nameof(metadata));

        ArgumentNullException.ThrowIfNull(
            instruments);

        InstrumentDescriptor[] instrumentArray =
            instruments.ToArray();

        if (instrumentArray.Any(
            instrument => instrument is null))
        {
            throw new ArgumentException(
                "The instrument collection must not contain null values.",
                nameof(instruments));
        }

        Instruments =
            instrumentArray;
    }

    /// <summary>
    /// Gets reusable endpoint metadata.
    /// </summary>
    public EndpointMetadata Metadata
    {
        get;
    }

    /// <summary>
    /// Gets reusable instrument descriptors in their declared order.
    /// </summary>
    public IReadOnlyList<InstrumentDescriptor> Instruments
    {
        get;
    }

    /// <summary>
    /// Creates an endpoint descriptor with an authoritative endpoint identity
    /// and this definition's reusable descriptor content.
    /// </summary>
    public EndpointDescriptor Materialize(
        EndpointId endpointId)
    {
        ArgumentNullException.ThrowIfNull(
            endpointId);

        return new EndpointDescriptor(
            endpointId,
            Instruments)
        {
            Metadata = Metadata
        };
    }
}