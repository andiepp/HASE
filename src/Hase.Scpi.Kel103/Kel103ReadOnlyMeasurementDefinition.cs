using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103;

public static class Kel103ReadOnlyMeasurementDefinition
{
    public static DescriptorReference Reference { get; } =
        new(Kel103IdentityDefinition.Reference.Id, version: 2);

    public static EndpointDescriptorDefinition EndpointDefinition { get; } = Create();

    private static EndpointDescriptorDefinition Create()
    {
        PropertyDescriptor Identity(string id, string path, string name) => new(
            new PropertyId(id), DescriptorPath.Parse(path), name, new StringDataDescriptor())
        { AccessMode = PropertyAccessMode.Read };

        PropertyDescriptor Measurement(
            Kel103MeasurementMapping mapping,
            string name,
            string quantityId,
            string quantityName,
            string unitId,
            string unitName)
        {
            var quantity = new Quantity(quantityId, quantityName);
            return new PropertyDescriptor(
                mapping.PropertyId,
                mapping.PropertyPath,
                name,
                new NumericDataDescriptor(
                    quantity,
                    new Unit(unitId, unitName, mapping.UnitSymbol, quantity)))
            { AccessMode = PropertyAccessMode.Read };
        }

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
            [
                Identity("product-identity", "Identity.Product", "Product identity"),
                Identity("firmware-version", "Identity.Firmware", "Firmware version"),
                Measurement(Kel103MeasurementMapping.Voltage, "Measured voltage", "electric-voltage", "Electric voltage", "volt", "Volt"),
                Measurement(Kel103MeasurementMapping.Current, "Measured current", "electric-current", "Electric current", "ampere", "Ampere"),
                Measurement(Kel103MeasurementMapping.Power, "Measured power", "power", "Power", "watt", "Watt")
            ])
        };

        return new EndpointDescriptorDefinition(
            new EndpointMetadata
            {
                DisplayName = "KEL-103 Electronic Load",
                Description = "Read-only KEL-103 endpoint definition."
            },
            [instrument]);
    }
}
