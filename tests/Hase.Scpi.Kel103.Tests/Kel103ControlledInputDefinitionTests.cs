using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103ControlledInputDefinitionTests
{
    [Fact]
    public void Reference_PreservesIdAndAdvancesOnlyVersionFive()
    {
        Assert.Equal(
            Kel103IdentityDefinition.Reference.Id,
            Kel103ControlledInputDefinition.Reference.Id);
        Assert.Equal((ushort)5, Kel103ControlledInputDefinition.Reference.Version);
        Assert.Equal((ushort)4, Kel103ControlledSetpointDefinition.Reference.Version);
        Assert.Equal((ushort)3, Kel103OperatingStateDefinition.Reference.Version);
        Assert.Equal((ushort)2, Kel103ReadOnlyMeasurementDefinition.Reference.Version);
        Assert.Equal((ushort)1, Kel103IdentityDefinition.Reference.Version);
    }

    [Fact]
    public void Definition_RetainsVersionFourPropertyContracts()
    {
        var versionFour = Assert.Single(
            Kel103ControlledSetpointDefinition.EndpointDefinition.Instruments).Interface.Properties;
        var versionFive = Assert.Single(
            Kel103ControlledInputDefinition.EndpointDefinition.Instruments).Interface.Properties;

        Assert.Equal(11, versionFive.Count);
        Assert.Equal(
            versionFour.Select(PropertyContract),
            versionFive.Select(PropertyContract));
        Assert.All(
            versionFive.Take(7),
            property => Assert.Equal(PropertyAccessMode.Read, property.AccessMode));
        Assert.All(
            versionFive.Skip(7),
            property => Assert.Equal(PropertyAccessMode.ReadWrite, property.AccessMode));
    }

    [Fact]
    public void Definition_AppendsExactInputCommandsAfterUnchangedModeCommands()
    {
        var versionFour = Assert.Single(
            Kel103ControlledSetpointDefinition.EndpointDefinition.Instruments).Interface.Commands;
        var versionFive = Assert.Single(
            Kel103ControlledInputDefinition.EndpointDefinition.Instruments).Interface.Commands;

        Assert.Equal(8, versionFive.Count);
        Assert.Equal(
            versionFour.Select(command => command.Path),
            versionFive.Take(5).Select(command => command.Path));
        Assert.Equal(
            new[]
            {
                "Mode.SelectConstantCurrent",
                "Mode.SelectConstantVoltage",
                "Mode.SelectConstantResistance",
                "Mode.SelectConstantPower",
                "Mode.SelectShortCircuit",
                "Input.Activate",
                "Input.Deactivate",
                "ShortCircuit.Activate"
            }.Select(DescriptorPath.Parse),
            versionFive.Select(command => command.Path));
        Assert.All(versionFive.Take(7), command => Assert.Null(command.Argument));
    }

    [Fact]
    public void ShortActivation_RequiresOneExplicitBooleanConfirmation()
    {
        var command = Assert.Single(
            Assert.Single(
                Kel103ControlledInputDefinition.EndpointDefinition.Instruments)
                .Interface.Commands,
            command => command.Path == DescriptorPath.Parse("ShortCircuit.Activate"));

        var argument = Assert.IsType<Hase.Core.Domain.Commands.CommandArgumentDescriptor>(
            command.Argument);
        Assert.Equal("Confirmation", argument.DisplayName);
        Assert.IsType<BooleanDataDescriptor>(argument.Data);
        string description = Assert.IsType<string>(argument.Description);
        Assert.Contains("true", description, StringComparison.Ordinal);
    }

    [Fact]
    public void Definition_HasNoEventsAndIsDistinctFromVersionFour()
    {
        var instrument = Assert.Single(
            Kel103ControlledInputDefinition.EndpointDefinition.Instruments);

        Assert.Empty(instrument.Interface.Events);
        Assert.NotSame(
            Kel103ControlledSetpointDefinition.EndpointDefinition,
            Kel103ControlledInputDefinition.EndpointDefinition);
        Assert.NotSame(
            Assert.Single(Kel103ControlledSetpointDefinition.EndpointDefinition.Instruments),
            instrument);
    }

    [Fact]
    public void Materialize_UsesExternalEndpointIdentity()
    {
        var endpointId = new EndpointId("configured-endpoint");

        Assert.Same(
            endpointId,
            Kel103ControlledInputDefinition.EndpointDefinition.Materialize(endpointId).Id);
    }

    private static object PropertyContract(PropertyDescriptor property)
    {
        object? numericContract = property.Data is NumericDataDescriptor numeric
            ? new
            {
                numeric.Quantity.Id,
                UnitId = numeric.NativeUnit.Id,
                numeric.NativeUnit.Symbol,
                numeric.Range,
                numeric.Resolution
            }
            : null;

        return new
        {
            property.Id,
            property.Path,
            property.DisplayName,
            property.AccessMode,
            DataType = property.Data.GetType(),
            Numeric = numericContract
        };
    }
}
