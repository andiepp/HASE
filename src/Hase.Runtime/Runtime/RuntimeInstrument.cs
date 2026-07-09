using Hase.Core.Domain.Instruments;

namespace Hase.Runtime.Runtime;

public sealed class RuntimeInstrument
{
    private readonly List<RuntimeProperty> _properties = [];

    public RuntimeInstrument(InstrumentDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

        foreach (var property in descriptor.Interface.Properties)
        {
            _properties.Add(new RuntimeProperty(property));
        }
    }

    public InstrumentDescriptor Descriptor { get; }

    public IReadOnlyList<RuntimeProperty> Properties => _properties;
}