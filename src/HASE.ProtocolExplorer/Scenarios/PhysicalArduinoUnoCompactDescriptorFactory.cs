using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Defines the host-side descriptor used by the physical Arduino Uno compact
/// command validation scenario.
/// </summary>
internal static class PhysicalArduinoUnoCompactDescriptorFactory
{
    public const byte ToggleBuiltInLedCompactCommandId =
        0x01;

    public static readonly DescriptorReference DescriptorReference =
        new(
            new DescriptorId(
                "arduino-uno-validation"),
            version: 1);

    public static readonly InstrumentId ControllerInstrumentId =
        new(
            "arduino-uno-controller-01");

    public static readonly DescriptorPath ToggleBuiltInLedCommandPath =
        new(
            "Led",
            "Toggle");

    public static EndpointDescriptorDefinition CreateDefinition()
    {
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
                            + "command."
                    },
                Interface =
                    new InstrumentInterface(
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
                    + "Compact Serial Protocol bootstrap and command execution."
            },
            instruments:
            [
                controller
            ]);
    }
}