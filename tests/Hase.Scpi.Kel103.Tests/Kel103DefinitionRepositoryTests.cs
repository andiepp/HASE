using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;

namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103DefinitionRepositoryTests
{
    [Fact]
    public async Task FindAsync_ResolvesVersionsOneThroughFiveExactly()
    {
        var repository = new Kel103DefinitionRepository();
        Assert.Same(Kel103IdentityDefinition.EndpointDefinition,
            await repository.FindAsync(Kel103IdentityDefinition.Reference));
        Assert.Same(Kel103ReadOnlyMeasurementDefinition.EndpointDefinition,
            await repository.FindAsync(Kel103ReadOnlyMeasurementDefinition.Reference));
        Assert.Same(Kel103OperatingStateDefinition.EndpointDefinition,
            await repository.FindAsync(Kel103OperatingStateDefinition.Reference));
        Assert.Same(Kel103ControlledSetpointDefinition.EndpointDefinition,
            await repository.FindAsync(Kel103ControlledSetpointDefinition.Reference));
        Assert.Same(Kel103ControlledInputDefinition.EndpointDefinition,
            await repository.FindAsync(Kel103ControlledInputDefinition.Reference));
    }

    [Fact]
    public async Task FindAsync_RejectsUnsupportedReference()
    {
        var repository = new Kel103DefinitionRepository();
        Assert.Null(await repository.FindAsync(
            new DescriptorReference(Kel103IdentityDefinition.Reference.Id, 6)));
        Assert.Null(await repository.FindAsync(
            new DescriptorReference(new DescriptorId("other"), 2)));
    }

    [Fact]
    public async Task IdentityRepository_RemainsVersionOneOnly()
    {
        var repository = new Kel103IdentityDefinitionRepository();
        Assert.Same(Kel103IdentityDefinition.EndpointDefinition,
            await repository.FindAsync(Kel103IdentityDefinition.Reference));
        Assert.Null(await repository.FindAsync(Kel103ReadOnlyMeasurementDefinition.Reference));
        Assert.Null(await repository.FindAsync(Kel103OperatingStateDefinition.Reference));
        Assert.Null(await repository.FindAsync(Kel103ControlledSetpointDefinition.Reference));
        Assert.Null(await repository.FindAsync(Kel103ControlledInputDefinition.Reference));
    }
}
