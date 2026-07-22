using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactEndpointOperationalDefinitionResolverTests
{
    [Fact]
    public void Constructor_NullRepository_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactEndpointOperationalDefinitionResolver(
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullCompatibilityValidator_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactEndpointOperationalDefinitionResolver(
                new StubRepository(
                    definition: null),
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task ResolveAsync_ExactCompatibleDefinition_ShouldReturnDefinition()
    {
        // Arrange
        DescriptorReference reference =
            CreateReference();

        EndpointDescriptorDefinition descriptorDefinition =
            CreateDescriptorDefinition(
                "Arduino Uno");

        CompactEndpointDefinition definition =
            new(
                reference,
                descriptorDefinition,
                []);

        var repository =
            new StubRepository(
                definition);

        var resolver =
            new CompactEndpointOperationalDefinitionResolver(
                repository);

        CompactEndpointAttachmentBootstrapResult bootstrapResult =
            CreateBootstrapResult(
                reference,
                descriptorDefinition);

        // Act
        CompactEndpointDefinition result =
            await resolver.ResolveAsync(
                bootstrapResult);

        // Assert
        Assert.Same(
            definition,
            result);

        Assert.Same(
            reference,
            repository.Reference);
    }

    [Fact]
    public async Task ResolveAsync_MissingDefinition_ShouldThrow()
    {
        // Arrange
        CompactEndpointAttachmentBootstrapResult bootstrapResult =
            CreateBootstrapResult(
                CreateReference(),
                CreateDescriptorDefinition(
                    "Arduino Uno"));

        var resolver =
            new CompactEndpointOperationalDefinitionResolver(
                new StubRepository(
                    definition: null));

        // Act
        Task Act()
        {
            return resolver.ResolveAsync(
                bootstrapResult);
        }

        // Assert
        await Assert.ThrowsAsync<CompactDescriptorNotFoundException>(
            Act);
    }

    [Fact]
    public async Task ResolveAsync_DifferentReturnedReference_ShouldThrow()
    {
        // Arrange
        DescriptorReference bootstrapReference =
            CreateReference();

        EndpointDescriptorDefinition descriptorDefinition =
            CreateDescriptorDefinition(
                "Arduino Uno");

        var returnedDefinition =
            new CompactEndpointDefinition(
                new DescriptorReference(
                    new DescriptorId(
                        "different-definition"),
                    version: 1),
                descriptorDefinition,
                []);

        var resolver =
            new CompactEndpointOperationalDefinitionResolver(
                new StubRepository(
                    returnedDefinition));

        // Act
        Task Act()
        {
            return resolver.ResolveAsync(
                CreateBootstrapResult(
                    bootstrapReference,
                    descriptorDefinition));
        }

        // Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task ResolveAsync_ChangedDescriptorDefinition_ShouldThrow()
    {
        // Arrange
        DescriptorReference reference =
            CreateReference();

        EndpointDescriptorDefinition bootstrapDefinition =
            CreateDescriptorDefinition(
                "Arduino Uno");

        EndpointDescriptorDefinition changedDefinition =
            CreateDescriptorDefinition(
                "Changed Arduino Uno");

        var resolver =
            new CompactEndpointOperationalDefinitionResolver(
                new StubRepository(
                    new CompactEndpointDefinition(
                        reference,
                        changedDefinition,
                        [])));

        // Act
        Task Act()
        {
            return resolver.ResolveAsync(
                CreateBootstrapResult(
                    reference,
                    bootstrapDefinition));
        }

        // Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task ResolveAsync_NullBootstrapResult_ShouldThrow()
    {
        // Arrange
        var resolver =
            new CompactEndpointOperationalDefinitionResolver(
                new StubRepository(
                    definition: null));

        // Act
        Task Act()
        {
            return resolver.ResolveAsync(
                null!);
        }

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task ResolveAsync_CancelledToken_ShouldNotQueryRepository()
    {
        // Arrange
        var repository =
            new StubRepository(
                definition: null);

        var resolver =
            new CompactEndpointOperationalDefinitionResolver(
                repository);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        Task Act()
        {
            return resolver.ResolveAsync(
                CreateBootstrapResult(
                    CreateReference(),
                    CreateDescriptorDefinition(
                        "Arduino Uno")),
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            Act);

        Assert.Equal(
            0,
            repository.CallCount);
    }

    private static CompactEndpointAttachmentBootstrapResult
        CreateBootstrapResult(
            DescriptorReference reference,
            EndpointDescriptorDefinition descriptorDefinition)
    {
        var endpointId =
            new EndpointId(
                "arduino-uno-01");

        EndpointDescriptor descriptor =
            descriptorDefinition.Materialize(
                endpointId);

        return new CompactEndpointAttachmentBootstrapResult(
            endpointId,
            reference,
            descriptorDefinition,
            descriptor);
    }

    private static DescriptorReference CreateReference()
    {
        return new DescriptorReference(
            new DescriptorId(
                "arduino-uno-validation"),
            version: 1);
    }

    private static EndpointDescriptorDefinition
        CreateDescriptorDefinition(
            string displayName)
    {
        return new EndpointDescriptorDefinition(
            new EndpointMetadata
            {
                DisplayName =
                    displayName
            },
            []);
    }

    private sealed class StubRepository
        : ICompactEndpointDefinitionRepository
    {
        private readonly CompactEndpointDefinition? _definition;

        public StubRepository(
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

        public int CallCount
        {
            get;
            private set;
        }

        public ValueTask<CompactEndpointDefinition?> FindAsync(
            DescriptorReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;

            Reference =
                reference;

            return ValueTask.FromResult(
                _definition);
        }
    }
}