using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Protocol.Serialization;

/// <summary>
/// Serializes and deserializes instrument interfaces using the HASE
/// protocol version 1 binary encoding.
/// </summary>
internal sealed class InstrumentInterfaceSerializer
{
    private readonly PropertyDescriptorSerializer _propertySerializer =
        new();

    private readonly CommandDescriptorSerializer _commandSerializer =
        new();

    private readonly EventDescriptorSerializer _eventSerializer =
        new();

    public void Write(
        BinaryProtocolWriter writer,
        InstrumentInterface instrumentInterface)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(instrumentInterface);

        writer.WriteCount(
            instrumentInterface.Properties.Count);

        foreach (PropertyDescriptor property in instrumentInterface.Properties)
        {
            _propertySerializer.Write(
                writer,
                property);
        }

        writer.WriteCount(
            instrumentInterface.Commands.Count);

        foreach (CommandDescriptor command in instrumentInterface.Commands)
        {
            _commandSerializer.Write(
                writer,
                command);
        }

        writer.WriteCount(
            instrumentInterface.Events.Count);

        foreach (EventDescriptor eventDescriptor in instrumentInterface.Events)
        {
            _eventSerializer.Write(
                writer,
                eventDescriptor);
        }
    }

    public InstrumentInterface Read(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        int propertyCount =
            reader.ReadCount();

        List<PropertyDescriptor> properties =
            new(propertyCount);

        for (int index = 0; index < propertyCount; index++)
        {
            properties.Add(
                _propertySerializer.Read(reader));
        }

        int commandCount =
            reader.ReadCount();

        List<CommandDescriptor> commands =
            new(commandCount);

        for (int index = 0; index < commandCount; index++)
        {
            commands.Add(
                _commandSerializer.Read(reader));
        }

        int eventCount =
            reader.ReadCount();

        List<EventDescriptor> events =
            new(eventCount);

        for (int index = 0; index < eventCount; index++)
        {
            events.Add(
                _eventSerializer.Read(reader));
        }

        return new InstrumentInterface(
            properties,
            commands,
            events);
    }
}
