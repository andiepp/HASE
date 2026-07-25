using DomainData = global::Hase.Core.Domain.Data;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one numeric data descriptor to its version 1 remote contract
/// representation.
/// </summary>
public interface INumericDataDescriptorMapper
{
    /// <summary>
    /// Maps one immutable numeric data descriptor.
    /// </summary>
    GrpcV1.NumericDataDescriptor Map(
        DomainData.NumericDataDescriptor descriptor);
}
