using CoreProperties = global::Hase.Core.Domain.Properties;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps runtime Property values to the version 1 remote contract.
/// </summary>
public interface IPropertyValueMapper
{
    /// <summary>
    /// Maps one timestamped Property value.
    /// </summary>
    GrpcV1.PropertyValue Map(
        CoreProperties.PropertyValue source);
}
