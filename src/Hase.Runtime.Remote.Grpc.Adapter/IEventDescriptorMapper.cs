using Domain = global::Hase.Core.Domain.Events;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one Event descriptor to its version 1 remote contract representation.
/// </summary>
public interface IEventDescriptorMapper
{
    /// <summary>
    /// Maps one immutable Event descriptor.
    /// </summary>
    GrpcV1.EventDescriptor Map(
        Domain.EventDescriptor descriptor);
}
