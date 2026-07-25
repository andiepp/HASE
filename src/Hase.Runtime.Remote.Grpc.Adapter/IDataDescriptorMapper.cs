using DomainData = global::Hase.Core.Domain.Data;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one data descriptor to its version 1 remote contract representation.
/// </summary>
public interface IDataDescriptorMapper
{
    /// <summary>
    /// Maps one immutable data descriptor.
    /// </summary>
    GrpcV1.DataDescriptor Map(
        DomainData.DataDescriptor descriptor);
}
