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
    private const byte AnalogInputVoltageCompactPropertyId = 0x02;
    private const byte ButtonPressedCompactEventId = 0x01;

    private static readonly DescriptorReference LegacyDescriptorReference =
        new(
            new DescriptorId("arduino-uno-validation"),
            version: 1);

    private static readonly DescriptorReference DescriptorReference =
        new(
            new DescriptorId("arduino-uno-validation"),
            version: 2);

    private static readonly InstrumentId ControllerInstrumentId =
        new("arduino-uno-controller-01");

    private static readonly PropertyId BuiltInLedStatePropertyId =
        new("built-in-led-state");

    private static readonly PropertyId AnalogInputVoltagePropertyId =
        new("analog-input-voltage");

    private static readonly DescriptorPath BuiltInLedStatePropertyPath =
        new("Led", "State");

    private static readonly DescriptorPath AnalogInputVoltagePropertyPath =
        new("Analog", "Voltage");

    private static readonly DescriptorPath ToggleBuiltInLedCommandPath =
        new("Led", "Toggle");

    private static readonly DescriptorPath ButtonPressedEventPath =
        new("Controller", "ButtonPressed");

    public static CompactEndpointDefinition Create()
    {
        EndpointDescriptorDefinition descriptorDefinition =
            CreateDescriptorDefinition(
                includeAnalogInput: true);

        return new CompactEndpointDefinition(
            DescriptorReference,
            descriptorDefinition,
            [
                new CompactPropertyMapping(
                    BuiltInLedStateCompactPropertyId,
                    ControllerInstrumentId,
                    BuiltInLedStatePropertyId,
                    CompactPropertyValueEncoding.Boolean),
                new CompactPropertyMapping(
                    AnalogInputVoltageCompactPropertyId,
                    ControllerInstrumentId,
                    AnalogInputVoltagePropertyId,
                    CompactPropertyValueEncoding.Unsigned16LittleEndianMillivolts)
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

    public static CompactEndpointDefinition CreateLegacy()
    {
        EndpointDescriptorDefinition descriptorDefinition =
            CreateDescriptorDefinition(
                includeAnalogInput: false);

        return new CompactEndpointDefinition(
            LegacyDescriptorReference,
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

    private static EndpointDescriptorDefinition CreateDescriptorDefinition(
        bool includeAnalogInput)
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

        var analogInputVoltage =
            new PropertyDescriptor(
                AnalogInputVoltagePropertyId,
                AnalogInputVoltagePropertyPath,
                "Analog Input Voltage",
                new NumericDataDescriptor(
                    Quantities.Voltage,
                    Units.Volt,
                    new ValueRange(
                        0.0,
                        5.0),
                    new Resolution(
                        5.0 / 1023.0)))
            {
                Description =
                    "Reports the voltage measured by the Arduino Uno A0 "
                    + "analog input.",
                AccessMode =
                    PropertyAccessMode.Read
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
                            + "event. Descriptor version 2 also exposes the "
                            + "A0 analog-input voltage."
                    },
                Interface =
                    new InstrumentInterface(
                        properties: includeAnalogInput
                            ? [builtInLedState, analogInputVoltage]
                            : [builtInLedState],
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
