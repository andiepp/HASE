using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
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
                        replace
                    ])
        };
    }
}
