using DomainEvents = global::Hase.Core.Domain.Events;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps Event descriptors to the version 1 remote contract.
/// </summary>
public sealed class EventDescriptorMapper
    : IEventDescriptorMapper
{
    private readonly IDataDescriptorMapper dataDescriptorMapper;

    public EventDescriptorMapper(
        IDataDescriptorMapper dataDescriptorMapper)
    {
        this.dataDescriptorMapper =
            dataDescriptorMapper
            ?? throw new ArgumentNullException(
                nameof(dataDescriptorMapper));
    }

    /// <inheritdoc />
    public GrpcV1.EventDescriptor Map(
        DomainEvents.EventDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(
            descriptor);

        var result =
            new GrpcV1.EventDescriptor
            {
                DisplayName =
                    descriptor.DisplayName
            };

        result.PathSegments.Add(
            descriptor.Path.Segments);

        if (descriptor.Description is not null)
        {
            result.Description =
                descriptor.Description;
        }

        if (descriptor.Payload is not null)
        {
            result.Payload =
                new GrpcV1.EventPayloadDescriptor
                {
                    DisplayName =
                        descriptor.Payload.DisplayName,
                    Data =
                        dataDescriptorMapper.Map(
                            descriptor.Payload.Data)
                };

            if (descriptor.Payload.Description is not null)
            {
                result.Payload.Description =
                    descriptor.Payload.Description;
            }
        }

        return result;
    }
}
