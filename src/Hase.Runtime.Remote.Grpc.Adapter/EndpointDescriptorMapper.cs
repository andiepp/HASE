using Domain = global::Hase.Core.Domain.Endpoints;
using DomainInstruments = global::Hase.Core.Domain.Instruments;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps endpoint descriptors to the version 1 remote contract.
/// </summary>
public sealed class EndpointDescriptorMapper
    : IEndpointDescriptorMapper
{
    private readonly IInstrumentDescriptorMapper instrumentDescriptorMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public EndpointDescriptorMapper(
        IInstrumentDescriptorMapper instrumentDescriptorMapper)
    {
        this.instrumentDescriptorMapper =
            instrumentDescriptorMapper
            ?? throw new ArgumentNullException(
                nameof(instrumentDescriptorMapper));
    }

    /// <inheritdoc />
    public GrpcV1.EndpointDescriptor Map(
        Domain.EndpointDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(
            descriptor);

        var result =
            new GrpcV1.EndpointDescriptor
            {
                EndpointId =
                    descriptor.Id.Value
            };

        if (descriptor.Metadata.DisplayName is not null)
        {
            result.DisplayName =
                descriptor.Metadata.DisplayName;
        }

        if (descriptor.Metadata.Description is not null)
        {
            result.Description =
                descriptor.Metadata.Description;
        }

        foreach (DomainInstruments.InstrumentDescriptor instrument in
                 descriptor.Instruments)
        {
            GrpcV1.InstrumentDescriptor mappedInstrument =
                instrumentDescriptorMapper.Map(
                    instrument)
                ?? throw new InvalidOperationException(
                    "The instrument descriptor mapper returned null.");

            result.Instruments.Add(
                mappedInstrument);
        }

        return result;
    }
}
