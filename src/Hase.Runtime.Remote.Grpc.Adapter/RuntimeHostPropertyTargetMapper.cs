using Hase.Core.Domain.Identity;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps version 1 remote Property targets to generation-scoped northbound
/// runtime-host targets.
/// </summary>
public sealed class RuntimeHostPropertyTargetMapper
    : IRuntimeHostPropertyTargetMapper
{
    /// <inheritdoc />
    public Northbound.RuntimeHostPropertyTarget Map(
        GrpcV1.PropertyTarget source)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        return new Northbound.RuntimeHostPropertyTarget(
            new EndpointId(
                source.EndpointId),
            new Northbound.RuntimeEndpointAttachmentGeneration(
                Guid.Parse(
                    source.AttachmentGeneration)),
            new InstrumentId(
                source.InstrumentId),
            new PropertyId(
                source.PropertyId));
    }
}
