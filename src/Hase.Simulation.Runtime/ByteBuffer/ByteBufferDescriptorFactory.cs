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

    public static readonly DescriptorPath ValuePropertyPath =
        new(
            "Buffer",
            "Value");

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
                    PropertyAccessMode.Read
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
            "Simulated Byte Buffer",
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
                        "Opt-in deterministic ByteArray command validation."
                },
            Interface =
                new InstrumentInterface(
                    properties:
                    [
                        value
                    ],
                    commands:
                    [
                        replace
                    ])
        };
    }
}
