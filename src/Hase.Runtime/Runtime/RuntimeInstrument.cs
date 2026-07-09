using Hase.Core.Domain.Instruments;

namespace Hase.Runtime.Runtime;

/// <summary>
/// Runtime representation of an instrument.
/// It references the immutable instrument descriptor.
/// </summary>
public sealed class RuntimeInstrument
{
    public RuntimeInstrument(InstrumentDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    /// <summary>
    /// Static descriptor of this instrument.
    /// </summary>
    public InstrumentDescriptor Descriptor { get; }
}