using Hase.Core.Domain.Commands;
using System.Diagnostics.Metrics;

namespace Hase.Runtime.Runtime;

public sealed class RuntimeCommand
{
    public RuntimeCommand(RuntimeInstrument instrument, CommandDescriptor descriptor)
    {
        Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public CommandDescriptor Descriptor { get; }
    public RuntimeInstrument Instrument { get; }
}