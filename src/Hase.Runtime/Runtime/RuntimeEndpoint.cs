using Hase.Core.Domain.Endpoints;

namespace Hase.Runtime.Runtime;

/// <summary>
/// Runtime representation of an endpoint.
/// It references the immutable endpoint descriptor.
/// </summary>
public sealed class RuntimeEndpoint
{
    private readonly List<RuntimeInstrument> _instruments = [];

    public RuntimeEndpoint(EndpointDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

        // Automatically build the runtime graph
        foreach (var instrument in descriptor.Instruments)
        {
            _instruments.Add(new RuntimeInstrument(instrument));
        }
    }

    /// <summary>
    /// Static descriptor of this endpoint.
    /// </summary>
    public EndpointDescriptor Descriptor { get; }

    /// <summary>
    /// Runtime instruments belonging to this endpoint.
    /// </summary>
    public IReadOnlyList<RuntimeInstrument> Instruments => _instruments;
}