using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103InputControlMappingTests
{
    [Theory]
    [InlineData(0, "Input.Activate", true, false, ":INPut ON")]
    [InlineData(1, "Input.Deactivate", false, false, ":INPut OFF")]
    [InlineData(2, "ShortCircuit.Activate", true, true, ":INPut ON")]
    public void Mapping_HasExactCharacterizedMetadata(
        int index,
        string commandPath,
        bool inputEnabled,
        bool requiresConfirmation,
        string command)
    {
        Kel103InputControlMapping mapping = Kel103InputControlMapping.All[index];

        Assert.Equal(DescriptorPath.Parse(commandPath), mapping.CommandPath);
        Assert.Equal(inputEnabled, mapping.InputEnabled);
        Assert.Equal(requiresConfirmation, mapping.RequiresConfirmation);
        Assert.Equal(command, mapping.Command);
    }

    [Fact]
    public void Commands_AreFixedMutationsWithoutFramingOrQuerySyntax()
    {
        Assert.All(Kel103InputControlMapping.All, mapping =>
        {
            Assert.StartsWith(":INPut ", mapping.Command, StringComparison.Ordinal);
            Assert.DoesNotContain('?', mapping.Command);
            Assert.DoesNotContain('\r', mapping.Command);
            Assert.DoesNotContain('\n', mapping.Command);
        });
    }

    [Fact]
    public void All_UsesUniquePathsAndIsReadOnly()
    {
        Assert.Equal(
            Kel103InputControlMapping.All.Count,
            Kel103InputControlMapping.All.Select(mapping => mapping.CommandPath).Distinct().Count());
        var mappings = Assert.IsAssignableFrom<IList<Kel103InputControlMapping>>(
            Kel103InputControlMapping.All);
        Assert.True(mappings.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mappings.Add(
            Kel103InputControlMapping.Activate));
    }
}
