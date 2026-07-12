using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Execution;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Protocol;

/// <summary>
/// Dispatches protocol requests to one runtime endpoint.
/// </summary>
public sealed class RuntimeProtocolDispatcher
    : IRuntimeProtocolDispatcher
{
    private readonly RuntimeEndpoint _endpoint;

    public RuntimeProtocolDispatcher(
        RuntimeEndpoint endpoint)
    {
        _endpoint = endpoint
            ?? throw new ArgumentNullException(
                nameof(endpoint));
    }

    public Task<DiscoverResponse> DispatchAsync(
        DiscoverRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(
            request,
            cancellationToken);

        IReadOnlyList<InstrumentId> instrumentIds =
            _endpoint.Instruments
                .Select(
                    instrument =>
                        instrument.Descriptor.Id)
                .ToArray();

        var response =
            new DiscoverResponse(
                request.CorrelationId,
                _endpoint.Descriptor.Id,
                instrumentIds);

        return Task.FromResult(response);
    }

    public Task<ReadEndpointDescriptorResponse> DispatchAsync(
        ReadEndpointDescriptorRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(
            request,
            cancellationToken);

        if (request.EndpointId !=
            _endpoint.Descriptor.Id)
        {
            var notFoundResponse =
                new ReadEndpointDescriptorResponse(
                    request.CorrelationId,
                    ProtocolResult.NotFound,
                    null);

            return Task.FromResult(
                notFoundResponse);
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
        Validate(
            request,
            cancellationToken);

        if (!TryResolveProperty(
                request.InstrumentId,
                request.PropertyId,
                out _,
                out RuntimeProperty? runtimeProperty))
        {
            var notFoundResponse =
                new ReadPropertyResponse(
                    request.CorrelationId,
                    ProtocolResult.NotFound,
                    null);

            return Task.FromResult(
                notFoundResponse);
        }

        var response =
            new ReadPropertyResponse(
                request.CorrelationId,
                ProtocolResult.Success,
                runtimeProperty.CurrentValue);

        return Task.FromResult(response);
    }

    public async Task<WritePropertyResponse> DispatchAsync(
        WritePropertyRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(
            request,
            cancellationToken);

        if (!TryResolveProperty(
                request.InstrumentId,
                request.PropertyId,
                out RuntimeInstrument? runtimeInstrument,
                out _))
        {
            return new WritePropertyResponse(
                request.CorrelationId,
                ProtocolResult.NotFound,
                null);
        }

        ExecutionResult executionResult =
            await runtimeInstrument.Executor
                .WritePropertyAsync(
                    request.PropertyId,
                    request.Value,
                    cancellationToken);

        if (!executionResult.Success)
        {
            return new WritePropertyResponse(
                request.CorrelationId,
                ProtocolResult.Rejected,
                null);
        }

        return new WritePropertyResponse(
            request.CorrelationId,
            ProtocolResult.Success,
            null);
    }

    private bool TryResolveProperty(
        InstrumentId instrumentId,
        PropertyId propertyId,
        out RuntimeInstrument? runtimeInstrument,
        out RuntimeProperty? runtimeProperty)
    {
        ArgumentNullException.ThrowIfNull(
            instrumentId);

        ArgumentNullException.ThrowIfNull(
            propertyId);

        runtimeInstrument =
            _endpoint.FindInstrument(
                instrumentId);

        if (runtimeInstrument is null)
        {
            runtimeProperty = null;
            return false;
        }

        runtimeProperty =
            runtimeInstrument.FindProperty(
                propertyId);

        return runtimeProperty is not null;
    }

    private static void Validate(
        ProtocolMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken
            .ThrowIfCancellationRequested();
    }
}