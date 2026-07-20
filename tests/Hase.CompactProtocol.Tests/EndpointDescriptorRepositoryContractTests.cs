using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;

namespace Hase.CompactProtocol.Tests;

public sealed class EndpointDescriptorRepositoryContractTests
{
    [Fact]
    public void Definition_Default_ShouldContainDefaultMetadataAndNoInstruments()
    {
        var definition =
            new EndpointDescriptorDefinition();

        Assert.Equal(
            new EndpointMetadata(),
            definition.Metadata);

        Assert.Empty(
            definition.Instruments);
    }

    [Fact]
    public void Definition_SuppliedValues_ShouldRetainMetadataAndInstrumentOrder()
    {
        var metadata =
            new EndpointMetadata
            {
                DisplayName = "Arduino Uno Environment Endpoint"
            };

        var first =
            CreateInstrument(
                "temperature");

        var second =
            CreateInstrument(
                "humidity");

        var definition =
            new EndpointDescriptorDefinition(
                metadata,
                [first, second]);

        Assert.Same(
            metadata,
            definition.Metadata);

        Assert.Equal(
            new[] { first, second },
            definition.Instruments);
    }

    [Fact]
    public void Definition_NullMetadata_ShouldThrow()
    {
        void Act()
        {
            _ = new EndpointDescriptorDefinition(
                null!,
                []);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Definition_NullInstrumentCollection_ShouldThrow()
    {
        void Act()
        {
            _ = new EndpointDescriptorDefinition(
                new EndpointMetadata(),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Definition_NullInstrumentElement_ShouldThrow()
    {
        void Act()
        {
            _ = new EndpointDescriptorDefinition(
                new EndpointMetadata(),
                [null!]);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public async Task Repository_FindAsync_ShouldReceiveExactReference()
    {
        var expectedReference =
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-environment"),
                version: 3);

        var expectedDefinition =
            new EndpointDescriptorDefinition();

        var repository =
            new TestEndpointDescriptorRepository(
                expectedDefinition);

        EndpointDescriptorDefinition? result =
            await repository.FindAsync(
                expectedReference);

        Assert.Same(
            expectedReference,
            repository.Reference);

        Assert.Same(
            expectedDefinition,
            result);
    }

    private static InstrumentDescriptor CreateInstrument(
        string value)
    {
        return new InstrumentDescriptor(
            new InstrumentId(
                value),
            value,
            new InstrumentKind(
                "sensor"));
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

            Reference =
                reference;

            return ValueTask.FromResult(
                _definition);
        }
    }
}
