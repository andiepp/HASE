using Hase.Core.Domain.Commands;
using System.Diagnostics.Metrics;

namespace Hase.Runtime.Runtime;

public sealed class RuntimeCommand : IRuntimeNode
{
    public RuntimeCommand(RuntimeInstrument instrument, CommandDescriptor descriptor)
    {
        Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public CommandDescriptor Descriptor { get; }
    public RuntimeInstrument Instrument { get; }

    public string DisplayName => Descriptor.DisplayName;
    public IRuntimeNode Parent => Instrument;

    public IReadOnlyList<IRuntimeNode> Children =>
        Array.Empty<IRuntimeNode>();
}