using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps version 1 remote Command targets to generation-scoped northbound
/// runtime-host targets.
/// </summary>
public sealed class RuntimeHostCommandTargetMapper
    : IRuntimeHostCommandTargetMapper
{
    /// <inheritdoc />
    public Northbound.RuntimeHostCommandTarget Map(
        GrpcV1.CommandTarget source)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        return new Northbound.RuntimeHostCommandTarget(
            new EndpointId(
                source.EndpointId),
            new Northbound.RuntimeEndpointAttachmentGeneration(
                Guid.Parse(
                    source.AttachmentGeneration)),
            new InstrumentId(
                source.InstrumentId),
            new DescriptorPath(
                source.CommandPathSegments));
    }
}
