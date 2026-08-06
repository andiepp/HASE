using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103;

public static class Kel103ControlledInputDefinition
{
    public static DescriptorReference Reference { get; } =
        new(Kel103IdentityDefinition.Reference.Id, version: 5);

    public static EndpointDescriptorDefinition EndpointDefinition { get; } = Create();

    private static EndpointDescriptorDefinition Create()
    {
        PropertyDescriptor StringProperty(string id, string path, string name) => new(
            new PropertyId(id), DescriptorPath.Parse(path), name, new StringDataDescriptor())
        { AccessMode = PropertyAccessMode.Read };

        PropertyDescriptor BooleanProperty(string id, string path, string name) => new(
            new PropertyId(id), DescriptorPath.Parse(path), name, new BooleanDataDescriptor())
        { AccessMode = PropertyAccessMode.Read };

        PropertyDescriptor NumericProperty(
            string id,
            string path,
            string name,
            string quantityId,
            string quantityName,
            string unitId,
            string unitName,
            string unitSymbol,
            PropertyAccessMode accessMode,
            ValueRange? range = null)
        {
            var quantity = new Quantity(quantityId, quantityName);
            return new PropertyDescriptor(
                new PropertyId(id),
                DescriptorPath.Parse(path),
                name,
                new NumericDataDescriptor(
                    quantity,
                    new Unit(unitId, unitName, unitSymbol, quantity),
                    range))
            { AccessMode = accessMode };
        }

        CommandDescriptor ParameterlessCommand(string path, string name) => new(
            DescriptorPath.Parse(path), name);

        var shortActivation = new CommandDescriptor(
            DescriptorPath.Parse("ShortCircuit.Activate"),
            "Activate short circuit",
            new CommandArgumentDescriptor(
                "Confirmation",
                new BooleanDataDescriptor())
            {
                Description = "The value true explicitly confirms SHORT activation."
            });

        var instrument = new InstrumentDescriptor(
            new InstrumentId("electronic-load-01"),
            "Electronic Load",
            new InstrumentKind("ElectronicLoad"))
        {
            Metadata = new InstrumentMetadata
            {
                Manufacturer = "KORAD",
                Model = Kel103IdentityDefinition.ProductIdentity,
                Description = "Programmable DC electronic load."
            },
            Interface = new InstrumentInterface(
                properties:
                [
                    StringProperty("product-identity", "Identity.Product", "Product identity"),
                    StringProperty("firmware-version", "Identity.Firmware", "Firmware version"),
                    NumericProperty("measured-voltage", "Measurement.Voltage", "Measured voltage", "electric-voltage", "Electric voltage", "volt", "Volt", "V", PropertyAccessMode.Read),
                    NumericProperty("measured-current", "Measurement.Current", "Measured current", "electric-current", "Electric current", "ampere", "Ampere", "A", PropertyAccessMode.Read),
                    NumericProperty("measured-power", "Measurement.Power", "Measured power", "power", "Power", "watt", "Watt", "W", PropertyAccessMode.Read),
                    StringProperty("operating-mode", "Operating.Mode", "Operating mode"),
                    BooleanProperty("input-enabled", "Input.Enabled", "Input enabled"),
                    NumericProperty("target-voltage", "Target.Voltage", "Target voltage", "electric-voltage", "Electric voltage", "volt", "Volt", "V", PropertyAccessMode.ReadWrite, new ValueRange(0.1, 120.0)),
                    NumericProperty("target-current", "Target.Current", "Target current", "electric-current", "Electric current", "ampere", "Ampere", "A", PropertyAccessMode.ReadWrite, new ValueRange(0.0, 30.0)),
                    NumericProperty("target-resistance", "Target.Resistance", "Target resistance", "electrical-resistance", "Electrical resistance", "ohm", "Ohm", "OHM", PropertyAccessMode.ReadWrite, new ValueRange(0.05, 7500.0)),
                    NumericProperty("target-power", "Target.Power", "Target power", "power", "Power", "watt", "Watt", "W", PropertyAccessMode.ReadWrite, new ValueRange(0.0, 300.0))
                ],
                commands:
                [
                    ParameterlessCommand("Mode.SelectConstantCurrent", "Select constant-current mode"),
                    ParameterlessCommand("Mode.SelectConstantVoltage", "Select constant-voltage mode"),
                    ParameterlessCommand("Mode.SelectConstantResistance", "Select constant-resistance mode"),
                    ParameterlessCommand("Mode.SelectConstantPower", "Select constant-power mode"),
                    ParameterlessCommand("Mode.SelectShortCircuit", "Select short-circuit mode"),
                    ParameterlessCommand("Input.Activate", "Activate input"),
                    ParameterlessCommand("Input.Deactivate", "Deactivate input"),
                    shortActivation
                ])
        };

        return new EndpointDescriptorDefinition(
            new EndpointMetadata
            {
                DisplayName = "KEL-103 Electronic Load",
                Description = "Controlled KEL-103 operating-state, setpoint, and input definition."
            },
            [instrument]);
    }
}
