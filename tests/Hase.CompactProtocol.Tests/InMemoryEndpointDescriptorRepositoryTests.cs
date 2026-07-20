using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;

namespace Hase.CompactProtocol.Tests;

public sealed class InMemoryEndpointDescriptorRepositoryTests
{
    [Fact]
    public async Task FindAsync_ExactReference_ShouldReturnDefinition()
    {
        DescriptorReference reference =
            CreateReference(
                version: 2);

        var definition =
            new EndpointDescriptorDefinition();

        var repository =
            CreateRepository(
                reference,
                definition);

        EndpointDescriptorDefinition? result =
            await repository.FindAsync(
                new DescriptorReference(
                    new DescriptorId(
                        reference.Id.Value),
                    reference.Version));

        Assert.Same(
            definition,
            result);
    }

    [Fact]
    public async Task FindAsync_DifferentVersion_ShouldReturnNull()
    {
        DescriptorReference reference =
            CreateReference(
                version: 1);

        var repository =
            CreateRepository(
                reference,
                new EndpointDescriptorDefinition());

        EndpointDescriptorDefinition? result =
            await repository.FindAsync(
                CreateReference(
                    version: 2));

        Assert.Null(
            result);
    }

    [Fact]
    public async Task Constructor_ShouldSnapshotEntries()
    {
        DescriptorReference reference =
            CreateReference(
                version: 1);

        var definition =
            new EndpointDescriptorDefinition();

        var entries =
            new List<KeyValuePair<
                DescriptorReference,
                EndpointDescriptorDefinition>>
            {
                new(
                    reference,
                    definition)
            };

        var repository =
            new InMemoryEndpointDescriptorRepository(
                entries);

        entries.Clear();

        EndpointDescriptorDefinition? result =
            await repository.FindAsync(
                reference);

        Assert.Same(
            definition,
            result);
    }

    [Fact]
    public void Constructor_DuplicateReference_ShouldThrow()
    {
        DescriptorReference firstReference =
            CreateReference(
                version: 1);

        var secondReference =
            new DescriptorReference(
                new DescriptorId(
                    firstReference.Id.Value),
                firstReference.Version);

        void Act()
        {
            _ = new InMemoryEndpointDescriptorRepository(
                new[]
                {
                    new KeyValuePair<
                        DescriptorReference,
                        EndpointDescriptorDefinition>(
                            firstReference,
                            new EndpointDescriptorDefinition()),
                    new KeyValuePair<
                        DescriptorReference,
                        EndpointDescriptorDefinition>(
                            secondReference,
                            new EndpointDescriptorDefinition())
                });
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_NullEntries_ShouldThrow()
    {
        void Act()
        {
            _ = new InMemoryEndpointDescriptorRepository(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullReferenceOrDefinition_ShouldThrow()
    {
        var definition =
            new EndpointDescriptorDefinition();

        Assert.Throws<ArgumentException>(
            () => new InMemoryEndpointDescriptorRepository(
                new[]
                {
                    new KeyValuePair<
                        DescriptorReference,
                        EndpointDescriptorDefinition>(
                            null!,
                            definition)
                }));

        Assert.Throws<ArgumentException>(
            () => new InMemoryEndpointDescriptorRepository(
                new[]
                {
                    new KeyValuePair<
                        DescriptorReference,
                        EndpointDescriptorDefinition>(
                            CreateReference(
                                version: 1),
                            null!)
                }));
    }

    [Fact]
    public async Task FindAsync_NullReference_ShouldThrow()
    {
        var repository =
            new InMemoryEndpointDescriptorRepository(
                []);

        async Task Act()
        {
            _ = await repository.FindAsync(
                null!);
        }

        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task FindAsync_CancelledToken_ShouldThrow()
    {
        var repository =
            new InMemoryEndpointDescriptorRepository(
                []);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await repository.FindAsync(
                CreateReference(
                    version: 1),
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);
    }

    private static InMemoryEndpointDescriptorRepository CreateRepository(
        DescriptorReference reference,
        EndpointDescriptorDefinition definition)
    {
        return new InMemoryEndpointDescriptorRepository(
            new[]
            {
                new KeyValuePair<
                    DescriptorReference,
                    EndpointDescriptorDefinition>(
                        reference,
                        definition)
            });
    }

    private static DescriptorReference CreateReference(
        ushort version)
    {
        return new DescriptorReference(
            new DescriptorId(
                "arduino-uno-environment"),
            version);
    }
}
