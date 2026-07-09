using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using System.Net;

namespace Hase.Runtime.Runtime;

public sealed class RuntimeInstrument
{
    private readonly List<RuntimeProperty> _properties = [];

    public RuntimeInstrument(RuntimeEndpoint endpoint, InstrumentDescriptor descriptor)
    {
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

        foreach (var property in descriptor.Interface.Properties)
        {
            _properties.Add(new RuntimeProperty(this, property));
        }

        foreach (var command in descriptor.Interface.Commands)
        {
            _commands.Add(new RuntimeCommand(this, command));
        }

        foreach (var eventDescriptor in descriptor.Interface.Events)
        {
            _events.Add(new RuntimeEvent(this, eventDescriptor));
        }
    }

    public InstrumentDescriptor Descriptor { get; }

    public RuntimeEndpoint Endpoint { get; }

    public IReadOnlyList<RuntimeProperty> Properties => _properties;

    private readonly List<RuntimeCommand> _commands = [];
    public IReadOnlyList<RuntimeCommand> Commands => _commands;

    private readonly List<RuntimeEvent> _events = [];
    public IReadOnlyList<RuntimeEvent> Events => _events;

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

    public RuntimeEvent? FindEvent(DescriptorPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return _events.FirstOrDefault(
            runtimeEvent => runtimeEvent.Descriptor.Path == path);
    }

    public bool UpdatePropertyValue(DescriptorPath path, PropertyValue value)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(value);

        var property = FindProperty(path);

        if (property is null)
        {
            return false;
        }

        property.UpdateValue(value);

        return true;
    }
}