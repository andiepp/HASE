using Domain = global::Hase.Core.Domain.Endpoints;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one endpoint descriptor to its version 1 remote contract
/// representation.
/// </summary>
public interface IEndpointDescriptorMapper
{
    /// <summary>
    /// Maps one immutable endpoint descriptor.
    /// </summary>
    GrpcV1.EndpointDescriptor Map(
        Domain.EndpointDescriptor descriptor);
}
