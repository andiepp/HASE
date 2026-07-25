using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps transport-independent endpoint snapshots to the version 1 remote
/// contract.
/// </summary>
public sealed class RuntimeEndpointSnapshotMapper
    : IRuntimeEndpointSnapshotMapper
{
    private readonly IEndpointDescriptorMapper descriptorMapper;
    private readonly IEndpointConnectionStatusMapper connectionStatusMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public RuntimeEndpointSnapshotMapper(
        IEndpointDescriptorMapper descriptorMapper,
        IEndpointConnectionStatusMapper connectionStatusMapper)
    {
        this.descriptorMapper =
            descriptorMapper
            ?? throw new ArgumentNullException(
                nameof(descriptorMapper));

        this.connectionStatusMapper =
            connectionStatusMapper
            ?? throw new ArgumentNullException(
                nameof(connectionStatusMapper));
    }

    /// <inheritdoc />
    public GrpcV1.PublishedRuntimeEndpointSnapshot Map(
        Northbound.PublishedRuntimeEndpointSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        GrpcV1.EndpointDescriptor descriptor =
            descriptorMapper.Map(
                snapshot.Descriptor)
            ?? throw new InvalidOperationException(
                "The endpoint descriptor mapper returned null.");

        GrpcV1.EndpointConnectionStatus connectionStatus =
            connectionStatusMapper.Map(
                snapshot.ConnectionStatus)
            ?? throw new InvalidOperationException(
                "The endpoint connection status mapper returned null.");

        return new GrpcV1.PublishedRuntimeEndpointSnapshot
        {
            EndpointId =
                snapshot.EndpointId.Value,
            AttachmentGeneration =
                snapshot.Generation.ToString(),
            Descriptor_ =
                descriptor,
            ConnectionStatus =
                connectionStatus
        };
    }
}
