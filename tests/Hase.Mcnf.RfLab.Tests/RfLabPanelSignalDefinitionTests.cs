using Hase.Core.Domain.Instruments;

namespace Hase.Mcnf.RfLab.Tests;

public sealed class RfLabPanelSignalDefinitionTests
{
    [Fact]
    public void Reference_ContinuesTheVersionLadderAdditively()
    {
        Assert.Equal(
            RfLabReadOnlyDefinition.Reference.Id,
            RfLabPanelSignalDefinition.Reference.Id);
        Assert.Equal(3, RfLabPanelSignalDefinition.Reference.Version);
    }

    [Fact]
    public void Definition_DeclaresTheOperatingPanel()
    {
        InstrumentDescriptor instrument =
            RfLabPanelSignalDefinition.EndpointDefinition.Instruments.Single();

        Assert.Equal(
            "rf-lab-signal-lab",
            instrument.Presentation?.PanelId);
        Assert.Equal(RfLabPanelDeclaration.PanelId, instrument.Presentation?.PanelId);
    }

    [Fact]
    public void Definition_KeepsTheControlledInterfaceUnchanged()
    {
        InstrumentDescriptor controlled =
            RfLabControlledSignalDefinition.EndpointDefinition.Instruments.Single();
        InstrumentDescriptor panel =
            RfLabPanelSignalDefinition.EndpointDefinition.Instruments.Single();

        Assert.Equal(controlled.Id, panel.Id);
        Assert.Equal(controlled.Kind, panel.Kind);
        Assert.Equal(controlled.Interface, panel.Interface);
        Assert.Equal(
            controlled.Interface.Properties.Count,
            panel.Interface.Properties.Count);
        Assert.Equal(
            controlled.Interface.Commands.Count,
            panel.Interface.Commands.Count);
    }

    [Fact]
    public void EarlierVersions_RemainWithoutADeclaration()
    {
        Assert.Null(
            RfLabReadOnlyDefinition.EndpointDefinition
                .Instruments.Single().Presentation);
        Assert.Null(
            RfLabControlledSignalDefinition.EndpointDefinition
                .Instruments.Single().Presentation);
    }

    [Fact]
    public async Task Repository_ServesTheExactPanelDefinition()
    {
        var repository = new RfLabDefinitionRepository();

        Assert.Same(
            RfLabPanelSignalDefinition.EndpointDefinition,
            await repository.FindAsync(RfLabPanelSignalDefinition.Reference));
    }
}
