using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Core.Domain.Commands;

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

        foreach (var command in descriptor.Interface.Commands)
        {
            _commands.Add(new RuntimeCommand(command));
        }
    }

    public InstrumentDescriptor Descriptor { get; }

    public IReadOnlyList<RuntimeProperty> Properties => _properties;

    private readonly List<RuntimeCommand> _commands = [];
    public IReadOnlyList<RuntimeCommand> Commands => _commands;

    public RuntimeProperty? FindProperty(DescriptorPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return _properties.FirstOrDefault(
            property => property.Descriptor.Path == path);
    }
    public RuntimeCommand? FindCommand(DescriptorPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return _commands.FirstOrDefault(
            command => command.Descriptor.Path == path);
    }

}