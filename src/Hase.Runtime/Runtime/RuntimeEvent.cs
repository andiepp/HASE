using Hase.Core.Domain.Events;

namespace Hase.Runtime.Runtime;

public sealed class RuntimeEvent
{
    public RuntimeEvent(EventDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public EventDescriptor Descriptor { get; }
}