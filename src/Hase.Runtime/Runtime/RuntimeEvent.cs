using Hase.Core.Domain.Events;
using System.Diagnostics.Metrics;

namespace Hase.Runtime.Runtime;

public sealed class RuntimeEvent
{
    public RuntimeEvent(RuntimeInstrument instrument, EventDescriptor descriptor)
    {
        Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public EventDescriptor Descriptor { get; }
    public RuntimeInstrument Instrument { get; }
}