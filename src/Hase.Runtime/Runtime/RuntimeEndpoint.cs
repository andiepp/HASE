using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Runtime;

/// <summary>
/// Runtime representation of an endpoint.
/// It references the immutable endpoint descriptor.
/// </summary>
public sealed class RuntimeEndpoint
{
    private readonly List<RuntimeInstrument> _instruments = [];

    public RuntimeEndpoint(RuntimeContext context, EndpointDescriptor descriptor)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

        foreach (var instrument in descriptor.Instruments)
        {
            _instruments.Add(new RuntimeInstrument(this, instrument));
        }
    }

    /// <summary>
    /// Static descriptor of this endpoint.
    /// </summary>
    public EndpointDescriptor Descriptor { get; }

    public RuntimeContext Context { get; }

    /// <summary>
    /// Runtime instruments belonging to this endpoint.
    /// </summary>
    public IReadOnlyList<RuntimeInstrument> Instruments => _instruments;

    public RuntimeInstrument? FindInstrument(InstrumentId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _instruments.FirstOrDefault(
            instrument => instrument.Descriptor.Id == id);
    }

}