using Domain = global::Hase.Core.Domain.Properties;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one Property descriptor to its version 1 remote contract
/// representation.
/// </summary>
public interface IPropertyDescriptorMapper
{
    /// <summary>
    /// Maps one immutable Property descriptor.
    /// </summary>
    GrpcV1.PropertyDescriptor Map(
        Domain.PropertyDescriptor descriptor);
}
