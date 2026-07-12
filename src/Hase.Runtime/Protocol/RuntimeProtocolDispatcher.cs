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

    public Task<ReadEndpointDescriptorResponse> DispatchAsync(
        ReadEndpointDescriptorRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request, cancellationToken);

        if (request.EndpointId != _endpoint.Descriptor.Id)
        {
            var notFoundResponse =
                new ReadEndpointDescriptorResponse(
                    request.CorrelationId,
                    ProtocolResult.NotFound,
                    null);

            return Task.FromResult(notFoundResponse);
        }

        var response =
            new ReadEndpointDescriptorResponse(
                request.CorrelationId,
                ProtocolResult.Success,
                _endpoint.Descriptor);

        return Task.FromResult(response);
    }

    public Task<ReadPropertyResponse> DispatchAsync(
        ReadPropertyRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request, cancellationToken);

        RuntimeInstrument? instrument =
            _endpoint.FindInstrument(request.InstrumentId);

        if (instrument is null)
        {
            var notFoundResponse =
                new ReadPropertyResponse(
                    request.CorrelationId,
                    ProtocolResult.NotFound,
                    null);

            return Task.FromResult(notFoundResponse);
        }

        RuntimeProperty? property =
            instrument.FindProperty(request.PropertyId);

        if (property is null)
        {
            var notFoundResponse =
                new ReadPropertyResponse(
                    request.CorrelationId,
                    ProtocolResult.NotFound,
                    null);

            return Task.FromResult(notFoundResponse);
        }

        var response =
            new ReadPropertyResponse(
                request.CorrelationId,
                ProtocolResult.Success,
                property.CurrentValue);

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