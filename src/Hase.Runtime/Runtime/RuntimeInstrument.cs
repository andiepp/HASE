using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Runtime;

public sealed class RuntimeInstrument : IPropertyValueObserver
{
    private readonly List<RuntimeProperty> _properties = [];
    private readonly List<RuntimeCommand> _commands = [];
    private readonly List<RuntimeEvent> _events = [];
    private readonly List<IPropertyValueObserver> _observers = [];

    public RuntimeInstrument(RuntimeEndpoint endpoint, InstrumentDescriptor descriptor)
    {
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

        foreach (var property in descriptor.Interface.Properties)
        {
            var runtimeProperty = new RuntimeProperty(this, property);
            runtimeProperty.Subscribe(this);
            _properties.Add(runtimeProperty);
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

    public RuntimeEndpoint Endpoint { get; }

    public InstrumentDescriptor Descriptor { get; }

    public IReadOnlyList<RuntimeProperty> Properties => _properties;

    public IReadOnlyList<RuntimeCommand> Commands => _commands;

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

    public bool UpdatePropertyValue(
        DescriptorPath path,
        PropertyValue value)
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

    public void Subscribe(IPropertyValueObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
        }
    }

    public void Unsubscribe(IPropertyValueObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        _observers.Remove(observer);
    }

    public void OnPropertyValueChanged(PropertyValueChanged change)
    {
        foreach (var observer in _observers)
        {
            observer.OnPropertyValueChanged(change);
        }
    }
}