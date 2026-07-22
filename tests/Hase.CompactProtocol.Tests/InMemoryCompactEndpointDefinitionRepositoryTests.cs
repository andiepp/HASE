using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;

namespace Hase.CompactProtocol.Tests;

public sealed class InMemoryCompactEndpointDefinitionRepositoryTests
{
    [Fact]
    public async Task FindAsync_ExactReference_ShouldReturnDefinition()
    {
        // Arrange
        CompactEndpointDefinition definition =
            CreateDefinition(
                version: 2);

        var repository =
            new InMemoryCompactEndpointDefinitionRepository(
                [
                    definition
                ]);

        var equivalentReference =
            new DescriptorReference(
                new DescriptorId(
                    definition.DescriptorReference.Id.Value),
                definition.DescriptorReference.Version);

        // Act
        CompactEndpointDefinition? result =
            await repository.FindAsync(
                equivalentReference);

        // Assert
        Assert.Same(
            definition,
            result);
    }

    [Fact]
    public async Task FindAsync_DifferentVersion_ShouldReturnNull()
    {
        // Arrange
        var repository =
            new InMemoryCompactEndpointDefinitionRepository(
                [
                    CreateDefinition(
                        version: 1)
                ]);

        // Act
        CompactEndpointDefinition? result =
            await repository.FindAsync(
                CreateReference(
                    version: 2));

        // Assert
        Assert.Null(
            result);
    }

    [Fact]
    public async Task Constructor_ShouldSnapshotDefinitions()
    {
        // Arrange
        CompactEndpointDefinition definition =
            CreateDefinition(
                version: 1);

        var definitions =
            new List<CompactEndpointDefinition>
            {
                definition
            };

        // Act
        var repository =
            new InMemoryCompactEndpointDefinitionRepository(
                definitions);

        definitions.Clear();

        CompactEndpointDefinition? result =
            await repository.FindAsync(
                definition.DescriptorReference);

        // Assert
        Assert.Same(
            definition,
            result);
    }

    [Fact]
    public void Constructor_DuplicateReference_ShouldThrow()
    {
        // Arrange
        CompactEndpointDefinition first =
            CreateDefinition(
                version: 1);

        var second =
            new CompactEndpointDefinition(
                new DescriptorReference(
                    new DescriptorId(
                        first.DescriptorReference.Id.Value),
                    first.DescriptorReference.Version),
                new EndpointDescriptorDefinition(),
                []);

        // Act
        void Act()
        {
            _ = new InMemoryCompactEndpointDefinitionRepository(
                [
                    first,
                    second
                ]);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_NullDefinitions_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new InMemoryCompactEndpointDefinitionRepository(
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullDefinitionEntry_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new InMemoryCompactEndpointDefinitionRepository(
                [
                    null!
                ]);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public async Task FindAsync_NullReference_ShouldThrow()
    {
        // Arrange
        var repository =
            new InMemoryCompactEndpointDefinitionRepository(
                []);

        // Act
        async Task Act()
        {
            _ = await repository.FindAsync(
                null!);
        }

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task FindAsync_CancelledToken_ShouldThrow()
    {
        // Arrange
        var repository =
            new InMemoryCompactEndpointDefinitionRepository(
                []);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        async Task Act()
        {
            _ = await repository.FindAsync(
                CreateReference(
                    version: 1),
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            Act);
    }

    private static CompactEndpointDefinition CreateDefinition(
        ushort version)
    {
        return new CompactEndpointDefinition(
            CreateReference(
                version),
            new EndpointDescriptorDefinition(),
            []);
    }

    private static DescriptorReference CreateReference(
        ushort version)
    {
        return new DescriptorReference(
            new DescriptorId(
                "arduino-uno-validation"),
            version);
    }
}