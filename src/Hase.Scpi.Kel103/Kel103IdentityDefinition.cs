using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103;

/// <summary>
/// Defines the first version of the normalized, identity-only KEL-103 endpoint.
/// </summary>
public static class Kel103IdentityDefinition
{
    public const string ProductIdentity = "KEL-103";

    public static DescriptorReference Reference { get; } =
        new(new DescriptorId("kel103-identity"), version: 1);

    public static EndpointDescriptorDefinition EndpointDefinition { get; } =
        CreateEndpointDefinition();

    private static EndpointDescriptorDefinition CreateEndpointDefinition()
    {
        var productIdentity = new PropertyDescriptor(
            new PropertyId("product-identity"),
            DescriptorPath.Parse("Identity.Product"),
            "Product identity",
            new StringDataDescriptor())
        {
            Description = "Verified instrument product identity.",
            AccessMode = PropertyAccessMode.Read
        };

        var firmwareVersion = new PropertyDescriptor(
            new PropertyId("firmware-version"),
            DescriptorPath.Parse("Identity.Firmware"),
            "Firmware version",
            new StringDataDescriptor())
        {
            Description = "Verified instrument firmware version.",
            AccessMode = PropertyAccessMode.Read
        };

        var instrument = new InstrumentDescriptor(
            new InstrumentId("electronic-load-01"),
            "Electronic Load",
            new InstrumentKind("ElectronicLoad"))
        {
            Metadata = new InstrumentMetadata
            {
                Manufacturer = "KORAD",
                Model = ProductIdentity,
                Description = "Programmable DC electronic load."
            },
            Interface = new InstrumentInterface([productIdentity, firmwareVersion])
        };

        return new EndpointDescriptorDefinition(
            new EndpointMetadata
            {
                DisplayName = "KEL-103 Electronic Load",
                Description = "Identity-only KEL-103 endpoint definition."
            },
            [instrument]);
    }
}
