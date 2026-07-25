using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps published runtime Property snapshots to the version 1 remote
/// contract.
/// </summary>
public sealed class PublishedRuntimePropertySnapshotMapper
    : IPublishedRuntimePropertySnapshotMapper
{
    private readonly IPropertyDescriptorMapper descriptorMapper;
    private readonly IEndpointConnectionStatusMapper connectionStatusMapper;
    private readonly IPropertyValueMapper propertyValueMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public PublishedRuntimePropertySnapshotMapper(
        IPropertyDescriptorMapper descriptorMapper,
        IEndpointConnectionStatusMapper connectionStatusMapper,
        IPropertyValueMapper propertyValueMapper)
    {
        this.descriptorMapper =
            descriptorMapper
            ?? throw new ArgumentNullException(
                nameof(descriptorMapper));

        this.connectionStatusMapper =
            connectionStatusMapper
            ?? throw new ArgumentNullException(
                nameof(connectionStatusMapper));

        this.propertyValueMapper =
            propertyValueMapper
            ?? throw new ArgumentNullException(
                nameof(propertyValueMapper));
    }

    /// <inheritdoc />
    public GrpcV1.PublishedRuntimePropertySnapshot Map(
        Northbound.PublishedRuntimePropertySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        GrpcV1.PropertyDescriptor descriptor =
            descriptorMapper.Map(
                snapshot.Descriptor)
            ?? throw new InvalidOperationException(
                "The Property descriptor mapper returned null.");

        GrpcV1.EndpointConnectionStatus connectionStatus =
            connectionStatusMapper.Map(
                snapshot.ConnectionStatus)
            ?? throw new InvalidOperationException(
                "The endpoint connection status mapper returned null.");

        var result =
            new GrpcV1.PublishedRuntimePropertySnapshot
            {
                Target =
                    new GrpcV1.PropertyTarget
                    {
                        EndpointId =
                            snapshot.Target.EndpointId.Value,
                        AttachmentGeneration =
                            snapshot.Target.AttachmentGeneration.ToString(),
                        InstrumentId =
                            snapshot.Target.InstrumentId.Value,
                        PropertyId =
                            snapshot.Target.PropertyId.Value
                    },
                Descriptor_ =
                    descriptor,
                ConnectionStatus =
                    connectionStatus
            };

        if (snapshot.CurrentValue is not null)
        {
            result.CurrentValue =
                propertyValueMapper.Map(
                    snapshot.CurrentValue)
                ?? throw new InvalidOperationException(
                    "The Property value mapper returned null.");
        }

        return result;
    }
}
