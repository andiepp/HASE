using Domain = global::Hase.Core.Domain.Commands;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one Command descriptor to its version 1 remote contract
/// representation.
/// </summary>
public interface ICommandDescriptorMapper
{
    /// <summary>
    /// Maps one immutable Command descriptor.
    /// </summary>
    GrpcV1.CommandDescriptor Map(
        Domain.CommandDescriptor descriptor);
}
