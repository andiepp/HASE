using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103ModeSelectionMappingTests
{
    [Theory]
    [InlineData(0, "Mode.SelectConstantCurrent", Kel103OperatingMode.ConstantCurrent, ":FUNCtion CC", "CC")]
    [InlineData(1, "Mode.SelectConstantVoltage", Kel103OperatingMode.ConstantVoltage, ":FUNCtion CV", "CV")]
    [InlineData(2, "Mode.SelectConstantResistance", Kel103OperatingMode.ConstantResistance, ":FUNCtion CR", "CR")]
    [InlineData(3, "Mode.SelectConstantPower", Kel103OperatingMode.ConstantPower, ":FUNCtion CW", "CW")]
    [InlineData(4, "Mode.SelectShortCircuit", Kel103OperatingMode.ShortCircuit, ":FUNCtion SHORt", "SHORt")]
    public void Mapping_HasExactCharacterizedMetadata(
        int index,
        string commandPath,
        Kel103OperatingMode mode,
        string command,
        string expectedReadbackToken)
    {
        Kel103ModeSelectionMapping mapping = Kel103ModeSelectionMapping.All[index];

        Assert.Equal(DescriptorPath.Parse(commandPath), mapping.CommandPath);
        Assert.Equal(mode, mapping.Mode);
        Assert.Equal(command, mapping.Command);
        Assert.Equal(expectedReadbackToken, mapping.ExpectedReadbackToken);
    }

    [Fact]
    public void All_CoversVersionFourModeCommandsInDescriptorOrder()
    {
        var commands = Assert.Single(
            Kel103ControlledSetpointDefinition.EndpointDefinition.Instruments).Interface.Commands;

        Assert.Equal(
            commands.Select(command => command.Path),
            Kel103ModeSelectionMapping.All.Select(mapping => mapping.CommandPath));
    }

    [Fact]
    public void All_UsesUniqueCommandPathsAndModes()
    {
        Assert.Equal(
            Kel103ModeSelectionMapping.All.Count,
            Kel103ModeSelectionMapping.All.Select(mapping => mapping.CommandPath).Distinct().Count());
        Assert.Equal(
            Kel103ModeSelectionMapping.All.Count,
            Kel103ModeSelectionMapping.All.Select(mapping => mapping.Mode).Distinct().Count());
    }

    [Fact]
    public void Commands_AreFixedMutationsWithoutFramingOrQuerySyntax()
    {
        Assert.All(Kel103ModeSelectionMapping.All, mapping =>
        {
            Assert.StartsWith(":FUNCtion ", mapping.Command, StringComparison.Ordinal);
            Assert.DoesNotContain('?', mapping.Command);
            Assert.DoesNotContain('\r', mapping.Command);
            Assert.DoesNotContain('\n', mapping.Command);
        });
    }

    [Fact]
    public void All_IsReadOnly()
    {
        var mappings = Assert.IsAssignableFrom<IList<Kel103ModeSelectionMapping>>(
            Kel103ModeSelectionMapping.All);

        Assert.True(mappings.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mappings.Add(
            Kel103ModeSelectionMapping.ConstantCurrent));
    }
}
