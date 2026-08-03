using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;

namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103IdentityDefinitionRepositoryTests
{
    [Fact]
    public async Task FindAsync_ResolvesExactSupportedReference()
    {
        var repository = new Kel103IdentityDefinitionRepository();

        EndpointDescriptorDefinition? definition = await repository.FindAsync(
            Kel103IdentityDefinition.Reference);

        Assert.Same(Kel103IdentityDefinition.EndpointDefinition, definition);
    }

    [Fact]
    public async Task FindAsync_DoesNotResolveDifferentIdOrVersion()
    {
        var repository = new Kel103IdentityDefinitionRepository();

        EndpointDescriptorDefinition? differentId = await repository.FindAsync(
            new DescriptorReference(new DescriptorId("different-definition"), 1));
        EndpointDescriptorDefinition? differentVersion = await repository.FindAsync(
            new DescriptorReference(Kel103IdentityDefinition.Reference.Id, 2));

        Assert.Null(differentId);
        Assert.Null(differentVersion);
    }

    [Fact]
    public async Task FindAsync_ForwardsCancellationAndNullGuards()
    {
        var repository = new Kel103IdentityDefinitionRepository();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await repository.FindAsync(
                Kel103IdentityDefinition.Reference,
                cancellation.Token));
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await repository.FindAsync(null!));
    }
}
