using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103IdentityDefinitionTests
{
    [Fact]
    public void Reference_IsStableVersionOneContract()
    {
        Assert.Equal("kel103-identity", Kel103IdentityDefinition.Reference.Id.Value);
        Assert.Equal((ushort)1, Kel103IdentityDefinition.Reference.Version);
    }

    [Fact]
    public void Definition_ContainsOneIdentityOnlyInstrument()
    {
        var instrument = Assert.Single(Kel103IdentityDefinition.EndpointDefinition.Instruments);

        Assert.Equal("electronic-load-01", instrument.Id.Value);
        Assert.Equal("Electronic Load", instrument.Name);
        Assert.Equal("ElectronicLoad", instrument.Kind.Name);
        Assert.Equal("KORAD", instrument.Metadata.Manufacturer);
        Assert.Equal("KEL-103", instrument.Metadata.Model);
        Assert.Null(instrument.Metadata.SerialNumber);
        Assert.Null(instrument.Metadata.FirmwareVersion);
        Assert.Empty(instrument.Interface.Commands);
        Assert.Empty(instrument.Interface.Events);
    }

    [Fact]
    public void Definition_ContainsExactReadOnlyStringProperties()
    {
        var instrument = Assert.Single(Kel103IdentityDefinition.EndpointDefinition.Instruments);
        var properties = instrument.Interface.Properties;

        Assert.Collection(
            properties,
            property => AssertProperty(
                property,
                "product-identity",
                "Identity.Product",
                "Product identity"),
            property => AssertProperty(
                property,
                "firmware-version",
                "Identity.Firmware",
                "Firmware version"));
    }

    [Fact]
    public void Materialize_UsesExternalEndpointIdentity()
    {
        var endpointId = new EndpointId("configured-kel-endpoint");

        var descriptor = Kel103IdentityDefinition.EndpointDefinition.Materialize(endpointId);

        Assert.Same(endpointId, descriptor.Id);
        Assert.Equal("KEL-103 Electronic Load", descriptor.Metadata.DisplayName);
        Assert.Same(
            Assert.Single(Kel103IdentityDefinition.EndpointDefinition.Instruments),
            Assert.Single(descriptor.Instruments));
    }

    [Fact]
    public void Definition_ContainsNoDeploymentReachabilityOrRuntimeIdentity()
    {
        IEnumerable<string?> metadata = new[]
        {
            Kel103IdentityDefinition.EndpointDefinition.Metadata.DisplayName,
            Kel103IdentityDefinition.EndpointDefinition.Metadata.Description
        }.Concat(
            Kel103IdentityDefinition.EndpointDefinition.Instruments.SelectMany(
                instrument => new[]
                {
                    instrument.Name,
                    instrument.Metadata.Manufacturer,
                    instrument.Metadata.Model,
                    instrument.Metadata.Description
                }));

        string text = string.Join(" ", metadata.Where(value => value is not null));

        Assert.DoesNotContain("COM", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SN:", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("serial", text, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertProperty(
        PropertyDescriptor property,
        string expectedId,
        string expectedPath,
        string expectedDisplayName)
    {
        Assert.Equal(expectedId, property.Id.Value);
        Assert.Equal(expectedPath, property.Path.ToString());
        Assert.Equal(expectedDisplayName, property.DisplayName);
        Assert.IsType<StringDataDescriptor>(property.Data);
        Assert.Equal(PropertyAccessMode.Read, property.AccessMode);
    }
}
