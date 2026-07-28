using Hase.CompactProtocol;
using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.DesktopHost.App.Physical;

internal static class ArduinoUnoCompactDefinitionFactory
{
    private const byte ToggleBuiltInLedCompactCommandId = 0x01;
    private const byte BuiltInLedStateCompactPropertyId = 0x01;
    private const byte ButtonPressedCompactEventId = 0x01;

    private static readonly DescriptorReference DescriptorReference =
        new(
            new DescriptorId("arduino-uno-validation"),
            version: 1);

    private static readonly InstrumentId ControllerInstrumentId =
        new("arduino-uno-controller-01");

    private static readonly PropertyId BuiltInLedStatePropertyId =
        new("built-in-led-state");

    private static readonly DescriptorPath BuiltInLedStatePropertyPath =
        new("Led", "State");

    private static readonly DescriptorPath ToggleBuiltInLedCommandPath =
        new("Led", "Toggle");

    private static readonly DescriptorPath ButtonPressedEventPath =
        new("Controller", "ButtonPressed");

    public static CompactEndpointDefinition Create()
    {
        EndpointDescriptorDefinition descriptorDefinition =
            CreateDescriptorDefinition();

        return new CompactEndpointDefinition(
            DescriptorReference,
            descriptorDefinition,
            [
                new CompactPropertyMapping(
                    BuiltInLedStateCompactPropertyId,
                    ControllerInstrumentId,
                    BuiltInLedStatePropertyId,
                    CompactPropertyValueEncoding.Boolean)
            ],
            [
                new CompactEventMapping(
                    ButtonPressedCompactEventId,
                    ControllerInstrumentId,
                    ButtonPressedEventPath,
                    CompactEventValueEncoding.None)
            ],
            [
                new CompactCommandMapping(
                    ToggleBuiltInLedCompactCommandId,
                    ControllerInstrumentId,
                    ToggleBuiltInLedCommandPath)
            ]);
    }

    private static EndpointDescriptorDefinition CreateDescriptorDefinition()
    {
        var builtInLedState =
            new PropertyDescriptor(
                BuiltInLedStatePropertyId,
                BuiltInLedStatePropertyPath,
                "Built-in LED State",
                new BooleanDataDescriptor())
            {
                Description =
                    "Reports and controls whether the Arduino Uno built-in "
                    + "LED is on.",
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };

        var toggleBuiltInLed =
            new CommandDescriptor(
                ToggleBuiltInLedCommandPath,
                "Toggle Built-in LED")
            {
                Description =
                    "Toggles the Arduino Uno built-in LED."
            };

        var buttonPressed =
            new EventDescriptor(
                ButtonPressedEventPath,
                "Button Pressed")
            {
                Description =
                    "Raised when the Arduino Uno validation pushbutton is "
                    + "pressed."
            };

        var controller =
            new InstrumentDescriptor(
                ControllerInstrumentId,
                "Arduino Uno GPIO Controller",
                new InstrumentKind("controller"))
            {
                Metadata =
                    new InstrumentMetadata
                    {
                        Manufacturer = "Arduino",
                        Model = "Uno",
                        Description =
                            "GPIO controller provided by the Arduino Uno. "
                            + "The built-in LED is exposed through a compact "
                            + "read/write property and command, and the "
                            + "validation pushbutton is exposed as a compact "
                            + "event."
                    },
                Interface =
                    new InstrumentInterface(
                        properties: [builtInLedState],
                        commands: [toggleBuiltInLed],
                        events: [buttonPressed])
            };

        return new EndpointDescriptorDefinition(
            new EndpointMetadata
            {
                DisplayName =
                    "Arduino Uno Compact Validation Endpoint",
                Description =
                    "Physical Arduino Uno-class endpoint used to validate "
                    + "Compact Serial Protocol bootstrap, command execution, "
                    + "property reading and writing, and event notification."
            },
            instruments: [controller]);
    }
}
