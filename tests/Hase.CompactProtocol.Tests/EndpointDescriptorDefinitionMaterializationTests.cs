using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;

namespace Hase.CompactProtocol.Tests;

public sealed class EndpointDescriptorDefinitionMaterializationTests
{
    [Fact]
    public void Materialize_ShouldUseAuthoritativeEndpointIdentity()
    {
        var endpointId =
            new EndpointId(
                "uno-01");

        var definition =
            new EndpointDescriptorDefinition();

        EndpointDescriptor descriptor =
            definition.Materialize(
                endpointId);

        Assert.Same(
            endpointId,
            descriptor.Id);
    }

    [Fact]
    public void Materialize_ShouldRetainMetadataAndInstrumentOrder()
    {
        var metadata =
            new EndpointMetadata
            {
                DisplayName = "Arduino Uno Environment Endpoint"
            };

        InstrumentDescriptor first =
            CreateInstrument(
                "temperature");

        InstrumentDescriptor second =
            CreateInstrument(
                "humidity");

        var definition =
            new EndpointDescriptorDefinition(
                metadata,
                new[] { first, second });

        EndpointDescriptor descriptor =
            definition.Materialize(
                new EndpointId(
                    "uno-01"));

        Assert.Same(
            metadata,
            descriptor.Metadata);

        Assert.Equal(
            new[] { first, second },
            descriptor.Instruments);
    }

    [Fact]
    public void Materialize_DifferentEndpointIdentities_ShouldCreateIndependentDescriptors()
    {
        var definition =
            new EndpointDescriptorDefinition();

        EndpointDescriptor first =
            definition.Materialize(
                new EndpointId(
                    "uno-01"));

        EndpointDescriptor second =
            definition.Materialize(
                new EndpointId(
                    "uno-02"));

        Assert.NotSame(
            first,
            second);

        Assert.Equal(
            "uno-01",
            first.Id.Value);

        Assert.Equal(
            "uno-02",
            second.Id.Value);
    }

    [Fact]
    public void Materialize_NullEndpointIdentity_ShouldThrow()
    {
        var definition =
            new EndpointDescriptorDefinition();

        void Act()
        {
            _ = definition.Materialize(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
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
}