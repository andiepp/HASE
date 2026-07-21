using Hase.CompactProtocol;
using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Defines the host-side descriptor and compact mappings used by the physical
/// Arduino Uno validation endpoint.
/// </summary>
internal static class PhysicalArduinoUnoCompactDescriptorFactory
{
    public const byte ToggleBuiltInLedCompactCommandId =
        0x01;

    public const byte BuiltInLedStateCompactPropertyId =
        0x01;

    public static readonly DescriptorReference DescriptorReference =
        new(
            new DescriptorId(
                "arduino-uno-validation"),
            version: 1);

    public static readonly InstrumentId ControllerInstrumentId =
        new(
            "arduino-uno-controller-01");

    public static readonly PropertyId BuiltInLedStatePropertyId =
        new(
            "built-in-led-state");

    public static readonly DescriptorPath BuiltInLedStatePropertyPath =
        new(
            "Led",
            "State");

    public static readonly DescriptorPath ToggleBuiltInLedCommandPath =
        new(
            "Led",
            "Toggle");

    public static EndpointDescriptorDefinition CreateDefinition()
    {
        var builtInLedState =
            new PropertyDescriptor(
                BuiltInLedStatePropertyId,
                BuiltInLedStatePropertyPath,
                "Built-in LED State",
                new BooleanDataDescriptor())
            {
                Description =
                    "Reports whether the Arduino Uno built-in LED is on.",
                AccessMode =
                    PropertyAccessMode.Read
            };

        var toggleBuiltInLed =
            new CommandDescriptor(
                ToggleBuiltInLedCommandPath,
                "Toggle Built-in LED")
            {
                Description =
                    "Toggles the Arduino Uno built-in LED."
            };

        var controller =
            new InstrumentDescriptor(
                ControllerInstrumentId,
                "Arduino Uno GPIO Controller",
                new InstrumentKind(
                    "controller"))
            {
                Metadata =
                    new InstrumentMetadata
                    {
                        Manufacturer =
                            "Arduino",
                        Model =
                            "Uno",
                        Description =
                            "GPIO controller provided by the Arduino Uno. "
                            + "The built-in LED is exposed through a compact "
                            + "property and command."
                    },
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            builtInLedState
                        ],
                        commands:
                        [
                            toggleBuiltInLed
                        ])
            };

        return new EndpointDescriptorDefinition(
            new EndpointMetadata
            {
                DisplayName =
                    "Arduino Uno Compact Validation Endpoint",
                Description =
                    "Physical Arduino Uno-class endpoint used to validate "
                    + "Compact Serial Protocol bootstrap, command execution, "
                    + "and property reading."
            },
            instruments:
            [
                controller
            ]);
    }

    public static CompactPropertyMap CreatePropertyMap(
        EndpointDescriptorDefinition descriptorDefinition)
    {
        ArgumentNullException.ThrowIfNull(
            descriptorDefinition);

        return new CompactPropertyMap(
            descriptorDefinition,
            mappings:
            [
                new CompactPropertyMapping(
                    BuiltInLedStateCompactPropertyId,
                    ControllerInstrumentId,
                    BuiltInLedStatePropertyId,
                    CompactPropertyValueEncoding.Boolean)
            ]);
    }
}