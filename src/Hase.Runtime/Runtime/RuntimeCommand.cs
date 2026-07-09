using Hase.Core.Domain.Commands;

namespace Hase.Runtime.Runtime;

public sealed class RuntimeCommand
{
    public RuntimeCommand(CommandDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public CommandDescriptor Descriptor { get; }
}