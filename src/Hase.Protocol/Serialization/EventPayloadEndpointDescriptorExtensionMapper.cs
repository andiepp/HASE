using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Protocol.Serialization;

/// <summary>
/// Maps typed Event payloads to and from endpoint descriptor extensions.
/// </summary>
internal sealed class EventPayloadEndpointDescriptorExtensionMapper
{
    public const byte ExtensionType = 0x02;

    private readonly DataDescriptorSerializer _dataDescriptorSerializer =
        new();

    public IReadOnlyList<EndpointDescriptorExtension> CreateExtensions(
        EndpointDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        List<EndpointDescriptorExtension> extensions = new();

        foreach (InstrumentDescriptor instrument in descriptor.Instruments)
        {
            foreach (EventDescriptor eventDescriptor in instrument.Interface.Events)
            {
                if (eventDescriptor.Payload is null)
                {
                    continue;
                }

                BinaryProtocolWriter writer = new();
                writer.WriteString(instrument.Id.Value);
                writer.WriteString(eventDescriptor.Path.ToString());
                writer.WriteString(eventDescriptor.Payload.DisplayName);
                ProtocolSerializationHelper.WriteOptionalString(
                    writer,
                    eventDescriptor.Payload.Description);
                _dataDescriptorSerializer.Write(
                    writer,
                    eventDescriptor.Payload.Data);

                extensions.Add(
                    new EndpointDescriptorExtension(
                        ExtensionType,
                        writer.ToArray()));
            }
        }

        return extensions;
    }

    public EndpointDescriptor ApplyExtensions(
        EndpointDescriptor descriptor,
        IReadOnlyList<EndpointDescriptorExtension> extensions)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(extensions);

        EndpointDescriptor current = descriptor;
        HashSet<EventTarget> targets = new();

        foreach (EndpointDescriptorExtension extension in extensions)
        {
            if (extension.Type != ExtensionType)
            {
                continue;
            }

            DecodedEventPayload decoded = Decode(extension);
            EventTarget target =
                new(
                    decoded.InstrumentId.Value,
                    decoded.EventPath.ToString());

            if (!targets.Add(target))
            {
                throw new InvalidDataException(
                    $"Duplicate Event payload extension for instrument " +
                    $"'{target.InstrumentId}' and Event '{target.EventPath}'.");
            }

            current = Apply(current, decoded);
        }

        return current;
    }

    private DecodedEventPayload Decode(
        EndpointDescriptorExtension extension)
    {
        BinaryProtocolReader reader = new(extension.Payload.ToArray());
        InstrumentId instrumentId = new(reader.ReadString());
        DescriptorPath eventPath = DescriptorPath.Parse(reader.ReadString());
        string displayName = reader.ReadString();
        string? description =
            ProtocolSerializationHelper.ReadOptionalString(reader);
        EventPayloadDescriptor payload =
            new(
                displayName,
                _dataDescriptorSerializer.Read(reader))
            {
                Description = description
            };

        reader.EnsureFullyConsumed();
        return new DecodedEventPayload(instrumentId, eventPath, payload);
    }

    private static EndpointDescriptor Apply(
        EndpointDescriptor descriptor,
        DecodedEventPayload decoded)
    {
        int instrumentIndex =
            FindInstrumentIndex(descriptor, decoded.InstrumentId);

        if (instrumentIndex < 0)
        {
            throw new InvalidDataException(
                $"Event payload extension targets unknown instrument " +
                $"'{decoded.InstrumentId.Value}'.");
        }

        InstrumentDescriptor instrument = descriptor.Instruments[instrumentIndex];
        int eventIndex = FindEventIndex(instrument, decoded.EventPath);

        if (eventIndex < 0)
        {
            throw new InvalidDataException(
                $"Event payload extension targets unknown Event " +
                $"'{decoded.EventPath}' on instrument " +
                $"'{decoded.InstrumentId.Value}'.");
        }

        EventDescriptor eventDescriptor =
            instrument.Interface.Events[eventIndex];

        if (eventDescriptor.Payload is not null)
        {
            throw new InvalidDataException(
                $"Event '{decoded.EventPath}' on instrument " +
                $"'{decoded.InstrumentId.Value}' already has a payload.");
        }

        EventDescriptor typedEvent =
            eventDescriptor with
            {
                Payload = decoded.Payload
            };
        EventDescriptor[] events = instrument.Interface.Events.ToArray();
        events[eventIndex] = typedEvent;

        InstrumentDescriptor updatedInstrument =
            instrument with
            {
                Interface =
                    new InstrumentInterface(
                        instrument.Interface.Properties,
                        instrument.Interface.Commands,
                        events)
            };
        InstrumentDescriptor[] instruments = descriptor.Instruments.ToArray();
        instruments[instrumentIndex] = updatedInstrument;

        return new EndpointDescriptor(descriptor.Id, instruments)
        {
            Metadata = descriptor.Metadata
        };
    }

    private static int FindInstrumentIndex(
        EndpointDescriptor descriptor,
        InstrumentId instrumentId)
    {
        for (int index = 0; index < descriptor.Instruments.Count; index++)
        {
            if (descriptor.Instruments[index].Id == instrumentId)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindEventIndex(
        InstrumentDescriptor instrument,
        DescriptorPath eventPath)
    {
        for (int index = 0; index < instrument.Interface.Events.Count; index++)
        {
            if (instrument.Interface.Events[index].Path == eventPath)
            {
                return index;
            }
        }

        return -1;
    }

    private sealed record DecodedEventPayload(
        InstrumentId InstrumentId,
        DescriptorPath EventPath,
        EventPayloadDescriptor Payload);

    private readonly record struct EventTarget(
        string InstrumentId,
        string EventPath);
}
