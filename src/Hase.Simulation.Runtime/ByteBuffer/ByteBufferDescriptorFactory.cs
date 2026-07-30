using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Simulation.Runtime.ByteBuffer;

/// <summary>
/// Creates the descriptor for the opt-in simulated ByteArray validation
/// instrument.
/// </summary>
public static class ByteBufferDescriptorFactory
{
    public static readonly InstrumentId InstrumentId =
        new(
            "byte-buffer-01");

    public static readonly PropertyId ValuePropertyId =
        new(
            "byte-buffer-01.buffer-value");

    public static readonly PropertyId EnabledPropertyId =
        new(
            "byte-buffer-01.enabled");

    public static readonly PropertyId SetpointPropertyId =
        new(
            "byte-buffer-01.setpoint");

    public static readonly PropertyId LabelPropertyId =
        new(
            "byte-buffer-01.label");

    public static readonly DescriptorPath ValuePropertyPath =
        new(
            "Buffer",
            "Value");

    public static readonly DescriptorPath EnabledPropertyPath =
        new(
            "Editor",
            "Enabled");

    public static readonly DescriptorPath SetpointPropertyPath =
        new(
            "Editor",
            "Setpoint");

    public static readonly DescriptorPath LabelPropertyPath =
        new(
            "Editor",
            "Label");

    public static readonly DescriptorPath ReplaceCommandPath =
        new(
            "Buffer",
            "Replace");

    public static readonly DescriptorPath EmitNoPayloadCommandPath =
        new(
            "Event Validation",
            "Emit No Payload");

    public static readonly DescriptorPath EmitBooleanCommandPath =
        new(
            "Event Validation",
            "Emit Boolean");

    public static readonly DescriptorPath EmitNumericCommandPath =
        new(
            "Event Validation",
            "Emit Numeric");

    public static readonly DescriptorPath EmitStringCommandPath =
        new(
            "Event Validation",
            "Emit String");

    public static readonly DescriptorPath EmitByteArrayCommandPath =
        new(
            "Event Validation",
            "Emit ByteArray");

    public static readonly DescriptorPath NoPayloadEventPath =
        new(
            "Event Validation",
            "No Payload");

    public static readonly DescriptorPath BooleanEventPath =
        new(
            "Event Validation",
            "Boolean");

    public static readonly DescriptorPath NumericEventPath =
        new(
            "Event Validation",
            "Numeric");

    public static readonly DescriptorPath StringEventPath =
        new(
            "Event Validation",
            "String");

    public static readonly DescriptorPath ByteArrayEventPath =
        new(
            "Event Validation",
            "ByteArray");

    public static InstrumentDescriptor CreateDescriptor()
    {
        var value =
            new PropertyDescriptor(
                ValuePropertyId,
                ValuePropertyPath,
                "Buffer Value",
                new ByteArrayDataDescriptor())
            {
                Description =
                    "Current opaque ByteArray buffer contents.",
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };

        var enabled =
            new PropertyDescriptor(
                EnabledPropertyId,
                EnabledPropertyPath,
                "Enabled",
                new BooleanDataDescriptor())
            {
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };

        var setpoint =
            new PropertyDescriptor(
                SetpointPropertyId,
                SetpointPropertyPath,
                "Setpoint",
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius,
                    new ValueRange(
                        ByteBufferSimulation.MinimumSetpoint,
                        ByteBufferSimulation.MaximumSetpoint)))
            {
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };

        var label =
            new PropertyDescriptor(
                LabelPropertyId,
                LabelPropertyPath,
                "Label",
                new StringDataDescriptor())
            {
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };

        var replace =
            new CommandDescriptor(
                ReplaceCommandPath,
                "Replace Buffer",
                new CommandArgumentDescriptor(
                    "Payload",
                    new ByteArrayDataDescriptor())
                {
                    Description =
                        "Opaque bytes that replace the current buffer."
                })
            {
                Description =
                    "Atomically replaces the simulated ByteArray buffer."
            };

        var emitNoPayload =
            CreateEventCommand(
                EmitNoPayloadCommandPath,
                "Emit No-Payload Event");
        var emitBoolean =
            CreateEventCommand(
                EmitBooleanCommandPath,
                "Emit Boolean Event");
        var emitNumeric =
            CreateEventCommand(
                EmitNumericCommandPath,
                "Emit Numeric Event");
        var emitString =
            CreateEventCommand(
                EmitStringCommandPath,
                "Emit String Event");
        var emitByteArray =
            CreateEventCommand(
                EmitByteArrayCommandPath,
                "Emit ByteArray Event");

        var noPayload =
            new EventDescriptor(
                NoPayloadEventPath,
                "No-Payload Event")
            {
                Description =
                    "Deterministic parameterless Event validation occurrence."
            };
        var boolean =
            CreatePayloadEvent(
                BooleanEventPath,
                "Boolean Event",
                "State",
                new BooleanDataDescriptor());
        var numeric =
            CreatePayloadEvent(
                NumericEventPath,
                "Numeric Event",
                "Temperature",
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius));
        var text =
            CreatePayloadEvent(
                StringEventPath,
                "String Event",
                "Message",
                new StringDataDescriptor());
        var bytes =
            CreatePayloadEvent(
                ByteArrayEventPath,
                "ByteArray Event",
                "Bytes",
                new ByteArrayDataDescriptor());

        return new InstrumentDescriptor(
            InstrumentId,
            "Simulated Property Editor Validation",
            new InstrumentKind(
                "byte-buffer"))
        {
            Metadata =
                new InstrumentMetadata
                {
                    Manufacturer =
                        "HASE",
                    Model =
                        "ByteArray Validation Buffer",
                    Description =
                        "Opt-in deterministic multi-type Property editing "
                        + "and ByteArray command validation."
                },
            Interface =
                new InstrumentInterface(
                    properties:
                    [
                        enabled,
                        setpoint,
                        label,
                        value
                    ],
                    commands:
                    [
                        replace,
                        emitNoPayload,
                        emitBoolean,
                        emitNumeric,
                        emitString,
                        emitByteArray
                    ],
                    events:
                    [
                        noPayload,
                        boolean,
                        numeric,
                        text,
                        bytes
                    ])
        };
    }

    private static CommandDescriptor CreateEventCommand(
        DescriptorPath path,
        string displayName)
    {
        return new CommandDescriptor(
            path,
            displayName)
        {
            Description =
                "Publishes one deterministic Event validation occurrence."
        };
    }

    private static EventDescriptor CreatePayloadEvent(
        DescriptorPath path,
        string displayName,
        string payloadDisplayName,
        DataDescriptor data)
    {
        return new EventDescriptor(
            path,
            displayName)
        {
            Description =
                "Deterministic typed Event validation occurrence.",
            Payload =
                new EventPayloadDescriptor(
                    payloadDisplayName,
                    data)
                {
                    Description =
                        "Deterministic payload used for local and remote "
                        + "presentation validation."
                }
        };
    }
}
