using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactEndpointDescriptorResolverTests
{
    [Fact]
    public async Task ResolveAsync_ExactReference_ShouldMaterializeAuthoritativeIdentity()
    {
        CompactBootstrapResponse bootstrapResponse =
            CreateBootstrapResponse();

        var definition =
            new EndpointDescriptorDefinition();

        var repository =
            new TestEndpointDescriptorRepository(
                definition);

        var resolver =
            new CompactEndpointDescriptorResolver(
                repository);

        EndpointDescriptor result =
            await resolver.ResolveAsync(
                bootstrapResponse);

        Assert.Same(
            bootstrapResponse.DescriptorReference,
            repository.Reference);

        Assert.Same(
            bootstrapResponse.EndpointId,
            result.Id);
    }

    [Fact]
    public async Task ResolveAsync_FoundDefinition_ShouldRetainDescriptorContent()
    {
        var metadata =
            new EndpointMetadata
            {
                DisplayName = "Arduino Uno Environment Endpoint"
            };

        var instrument =
            new InstrumentDescriptor(
                new InstrumentId(
                    "temperature"),
                "Temperature",
                new InstrumentKind(
                    "sensor"));

        var definition =
            new EndpointDescriptorDefinition(
                metadata,
                new[] { instrument });

        var resolver =
            new CompactEndpointDescriptorResolver(
                new TestEndpointDescriptorRepository(
                    definition));

        EndpointDescriptor result =
            await resolver.ResolveAsync(
                CreateBootstrapResponse());

        Assert.Same(
            metadata,
            result.Metadata);

        Assert.Equal(
            new[] { instrument },
            result.Instruments);
    }

    [Fact]
    public async Task ResolveAsync_MissingReference_ShouldThrowSemanticException()
    {
        CompactBootstrapResponse bootstrapResponse =
            CreateBootstrapResponse();

        var resolver =
            new CompactEndpointDescriptorResolver(
                new TestEndpointDescriptorRepository(
                    definition: null));

        async Task Act()
        {
            _ = await resolver.ResolveAsync(
                bootstrapResponse);
        }

        CompactDescriptorNotFoundException exception =
            await Assert.ThrowsAsync<CompactDescriptorNotFoundException>(
                Act);

        Assert.Same(
            bootstrapResponse.DescriptorReference,
            exception.DescriptorReference);

        Assert.Contains(
            "arduino-uno-environment",
            exception.Message);

        Assert.Contains(
            "3",
            exception.Message);
    }

    [Fact]
    public async Task ResolveAsync_CancelledToken_ShouldNotAccessRepository()
    {
        var repository =
            new TestEndpointDescriptorRepository(
                new EndpointDescriptorDefinition());

        var resolver =
            new CompactEndpointDescriptorResolver(
                repository);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await resolver.ResolveAsync(
                CreateBootstrapResponse(),
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.Equal(
            0,
            repository.CallCount);
    }

    [Fact]
    public void Constructor_NullRepository_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEndpointDescriptorResolver(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task ResolveAsync_NullBootstrapResponse_ShouldThrow()
    {
        var resolver =
            new CompactEndpointDescriptorResolver(
                new TestEndpointDescriptorRepository(
                    new EndpointDescriptorDefinition()));

        async Task Act()
        {
            _ = await resolver.ResolveAsync(
                null!);
        }

        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void DescriptorNotFoundException_NullReference_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactDescriptorNotFoundException(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void DescriptorNotFoundException_ShouldBeAnIOException()
    {
        var exception =
            new CompactDescriptorNotFoundException(
                CreateBootstrapResponse().DescriptorReference);

        Assert.IsAssignableFrom<IOException>(
            exception);
    }

    private static CompactBootstrapResponse CreateBootstrapResponse()
    {
        return new CompactBootstrapResponse(
            correlationId: 0x2A,
            endpointId: new EndpointId(
                "uno-01"),
            descriptorReference: new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-environment"),
                version: 3));
    }

    private sealed class TestEndpointDescriptorRepository
        : IEndpointDescriptorRepository
    {
        private readonly EndpointDescriptorDefinition? _definition;

        public TestEndpointDescriptorRepository(
            EndpointDescriptorDefinition? definition)
        {
            _definition =
                definition;
        }

        public int CallCount
        {
            get;
            private set;
        }

        public DescriptorReference? Reference
        {
            get;
            private set;
        }

        public ValueTask<EndpointDescriptorDefinition?> FindAsync(
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