using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Protocol;

/// <summary>
/// Dispatches protocol requests to one runtime endpoint.
/// </summary>
public sealed class RuntimeProtocolDispatcher
    : IRuntimeProtocolDispatcher
{
    private readonly RuntimeEndpoint _endpoint;

    public RuntimeProtocolDispatcher(RuntimeEndpoint endpoint)
    {
        _endpoint = endpoint
            ?? throw new ArgumentNullException(nameof(endpoint));
    }

    public Task<DiscoverResponse> DispatchAsync(
        DiscoverRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request, cancellationToken);

        IReadOnlyList<InstrumentId> instrumentIds =
            _endpoint.Instruments
                .Select(instrument => instrument.Descriptor.Id)
                .ToArray();

        var response = new DiscoverResponse(
            request.CorrelationId,
            _endpoint.Descriptor.Id,
            instrumentIds);

        return Task.FromResult(response);
    }

    private static void Validate(
    ProtocolMessage request,
    CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
    }
}