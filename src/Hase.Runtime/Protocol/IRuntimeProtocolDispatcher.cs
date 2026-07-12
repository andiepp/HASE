using Hase.Protocol;

namespace Hase.Runtime.Protocol;

/// <summary>
/// Dispatches protocol requests to one runtime endpoint.
/// </summary>
public interface IRuntimeProtocolDispatcher
{
    Task<DiscoverResponse> DispatchAsync(
        DiscoverRequest request,
        CancellationToken cancellationToken = default);

    Task<ReadEndpointDescriptorResponse> DispatchAsync(
        ReadEndpointDescriptorRequest request,
        CancellationToken cancellationToken = default);
}