using Domain = global::Hase.Core.Domain.Instruments;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one instrument descriptor to its version 1 remote contract
/// representation.
/// </summary>
public interface IInstrumentDescriptorMapper
{
    /// <summary>
    /// Maps one immutable instrument descriptor.
    /// </summary>
    GrpcV1.InstrumentDescriptor Map(
        Domain.InstrumentDescriptor descriptor);
}
