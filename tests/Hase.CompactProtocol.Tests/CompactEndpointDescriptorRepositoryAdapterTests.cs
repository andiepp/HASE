using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactEndpointDescriptorRepositoryAdapterTests
{
    [Fact]
    public void Constructor_NullRepository_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactEndpointDescriptorRepositoryAdapter(
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task FindAsync_ExistingDefinition_ShouldReturnDescriptorDefinition()
    {
        // Arrange
        DescriptorReference reference =
            CreateReference();

        var descriptorDefinition =
            new EndpointDescriptorDefinition();

        var compactDefinition =
            new CompactEndpointDefinition(
                reference,
                descriptorDefinition,
                []);

        var compactRepository =
            new StubCompactEndpointDefinitionRepository(
                compactDefinition);

        var repository =
            new CompactEndpointDescriptorRepositoryAdapter(
                compactRepository);

        // Act
        EndpointDescriptorDefinition? result =
            await repository.FindAsync(
                reference);

        // Assert
        Assert.Same(
            descriptorDefinition,
            result);

        Assert.Same(
            reference,
            compactRepository.Reference);
    }

    [Fact]
    public async Task FindAsync_MissingDefinition_ShouldReturnNull()
    {
        // Arrange
        var repository =
            new CompactEndpointDescriptorRepositoryAdapter(
                new StubCompactEndpointDefinitionRepository(
                    definition: null));

        // Act
        EndpointDescriptorDefinition? result =
            await repository.FindAsync(
                CreateReference());

        // Assert
        Assert.Null(
            result);
    }

    [Fact]
    public async Task FindAsync_CancelledToken_ShouldForwardCancellation()
    {
        // Arrange
        var compactRepository =
            new StubCompactEndpointDefinitionRepository(
                definition: null);

        var repository =
            new CompactEndpointDescriptorRepositoryAdapter(
                compactRepository);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        async Task Act()
        {
            _ = await repository.FindAsync(
                CreateReference(),
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            Act);

        Assert.True(
            compactRepository.ReceivedCancellationRequested);
    }

    private static DescriptorReference CreateReference()
    {
        return new DescriptorReference(
            new DescriptorId(
                "arduino-uno-validation"),
            version: 1);
    }

    private sealed class StubCompactEndpointDefinitionRepository
        : ICompactEndpointDefinitionRepository
    {
        private readonly CompactEndpointDefinition? _definition;

        public StubCompactEndpointDefinitionRepository(
            CompactEndpointDefinition? definition)
        {
            _definition =
                definition;
        }

        public DescriptorReference? Reference
        {
            get;
            private set;
        }

        public bool ReceivedCancellationRequested
        {
            get;
            private set;
        }

        public ValueTask<CompactEndpointDefinition?> FindAsync(
            DescriptorReference reference,
            CancellationToken cancellationToken = default)
        {
            Reference =
                reference;

            ReceivedCancellationRequested =
                cancellationToken.IsCancellationRequested;

            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                _definition);
        }
    }
}