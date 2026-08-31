using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;

namespace Hase.Mcnf.RfLab.Tests;

public sealed class RfLabDefinitionRepositoryTests
{
    [Fact]
    public async Task FindAsync_ServesTheExactDefinitionInstances()
    {
        var repository = new RfLabDefinitionRepository();

        EndpointDescriptorDefinition? readOnly =
            await repository.FindAsync(RfLabReadOnlyDefinition.Reference);
        EndpointDescriptorDefinition? controlled =
            await repository.FindAsync(RfLabControlledSignalDefinition.Reference);

        Assert.Same(RfLabReadOnlyDefinition.EndpointDefinition, readOnly);
        Assert.Same(RfLabControlledSignalDefinition.EndpointDefinition, controlled);
        Assert.Same(
            RfLabPanelSignalDefinition.EndpointDefinition,
            await repository.FindAsync(RfLabPanelSignalDefinition.Reference));
    }

    [Fact]
    public async Task FindAsync_ReturnsNullForUnknownReferences()
    {
        var repository = new RfLabDefinitionRepository();

        Assert.Null(await repository.FindAsync(
            new DescriptorReference(new DescriptorId("rflab-signal-lab"), version: 4)));
        Assert.Null(await repository.FindAsync(
            new DescriptorReference(new DescriptorId("unknown"), version: 1)));
    }
}
