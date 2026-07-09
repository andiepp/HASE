using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;

namespace Hase.Core.Domain.Endpoints;

public sealed record EndpointDescriptor
{
    public EndpointDescriptor(
        EndpointId id,
        IEnumerable<InstrumentDescriptor>? instruments = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));

        Instruments = (instruments ?? Enumerable.Empty<InstrumentDescriptor>())
            .ToArray();
    }

    public EndpointId Id { get; }

    public EndpointMetadata Metadata { get; init; } = new();

    public IReadOnlyList<InstrumentDescriptor> Instruments { get; }
}