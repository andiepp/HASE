using Hase.CompactProtocol;
using Hase.Core.Domain.Endpoints;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;

namespace Hase.Runtime.Transport;

/// <summary>
/// Coordinates connection establishment, descriptor validation, property
/// synchronization, connection replacement, and shutdown for one compact
/// runtime endpoint.
/// </summary>
internal sealed class CompactRuntimeEndpointConnectionCoordinator
    : IAsyncDisposable
{
    private readonly ICompactEndpointConnectionFactory _connectionFactory;
    private readonly SerialTransportOptions _transportOptions;
    private readonly CompactPropertyMap _propertyMap;
    private readonly RuntimeEndpoint _runtimeEndpoint;
    private readonly EndpointDescriptorCompatibilityValidator
        _compatibilityValidator;
    private readonly CompactEndpointConnectionOwner _connectionOwner;
    private readonly TimeProvider _timeProvider;

    private readonly SemaphoreSlim _gate =
        new(
            initialCount: 1,
            maxCount: 1);

    private bool _disposed;

    public CompactRuntimeEndpointConnectionCoordinator(
        ICompactEndpointConnectionFactory connectionFactory,
        SerialTransportOptions transportOptions,
        CompactPropertyMap propertyMap,
        RuntimeEndpoint runtimeEndpoint,
        EndpointDescriptorCompatibilityValidator compatibilityValidator)
        : this(
            connectionFactory,
            transportOptions,
            propertyMap,
            runtimeEndpoint,
            compatibilityValidator,
            new CompactEndpointConnectionOwner(),
            TimeProvider.System)
    {
    }

    internal CompactRuntimeEndpointConnectionCoordinator(
        ICompactEndpointConnectionFactory connectionFactory,
        SerialTransportOptions transportOptions,
        CompactPropertyMap propertyMap,
        RuntimeEndpoint runtimeEndpoint,
        EndpointDescriptorCompatibilityValidator compatibilityValidator,
        CompactEndpointConnectionOwner connectionOwner,
        TimeProvider timeProvider)
    {
        _connectionFactory =
            connectionFactory
            ?? throw new ArgumentNullException(
                nameof(connectionFactory));

        _transportOptions =
            transportOptions
            ?? throw new ArgumentNullException(
                nameof(transportOptions));

        _propertyMap =
            propertyMap
            ?? throw new ArgumentNullException(
                nameof(propertyMap));

        _runtimeEndpoint =
            runtimeEndpoint
            ?? throw new ArgumentNullException(
                nameof(runtimeEndpoint));

        _compatibilityValidator =
            compatibilityValidator
            ?? throw new ArgumentNullException(
                nameof(compatibilityValidator));

        _connectionOwner =
            connectionOwner
            ?? throw new ArgumentNullException(
                nameof(connectionOwner));

        _timeProvider =
            timeProvider
            ?? throw new ArgumentNullException(
                nameof(timeProvider));
    }

    public RuntimeEndpoint RuntimeEndpoint =>
        _runtimeEndpoint;

    public SerialTransportOptions TransportOptions =>
        _transportOptions;

    public CompactEndpointConnection? ActiveConnection =>
        _connectionOwner.Current;

    public Task ConnectAsync(
        CancellationToken cancellationToken = default)
    {
        return EstablishAsync(
            "Connecting compact serial endpoint.",
            cancellationToken);
    }

    public Task ReconnectAsync(
        CancellationToken cancellationToken = default)
    {
        return EstablishAsync(
            "Reconnecting compact serial endpoint.",
            cancellationToken);
    }

    public void MarkFaulted(
        string message)
    {
        if (string.IsNullOrWhiteSpace(
                message))
        {
            throw new ArgumentException(
                "A compact endpoint fault message must not be empty.",
                nameof(message));
        }

        _runtimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Faulted,
                _timeProvider.GetUtcNow(),
                message.Trim()));
    }

    private async Task EstablishAsync(
        string connectingMessage,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(
            cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);

            cancellationToken.ThrowIfCancellationRequested();

            _runtimeEndpoint.UpdateConnectionStatus(
                new EndpointConnectionStatus(
                    EndpointConnectionState.Connecting,
                    _timeProvider.GetUtcNow(),
                    connectingMessage));

            CompactEndpointConnection? candidate =
                null;

            try
            {
                candidate =
                    await _connectionFactory.ConnectAsync(
                        _transportOptions,
                        _runtimeEndpoint.Descriptor.Id,
                        cancellationToken);

                _compatibilityValidator.Validate(
                    _runtimeEndpoint.Descriptor,
                    candidate.Descriptor);

                _runtimeEndpoint.UpdateConnectionStatus(
                    new EndpointConnectionStatus(
                        EndpointConnectionState.Synchronizing,
                        _timeProvider.GetUtcNow(),
                        "Synchronizing compact endpoint properties."));

                var synchronizer =
                    new CompactRuntimePropertySynchronizer(
                        candidate.Connection,
                        _propertyMap);

                IReadOnlyList<
                    CompactRuntimePropertySynchronizationResult> results =
                    await synchronizer.SynchronizeAsync(
                        _runtimeEndpoint,
                        cancellationToken);

                CompactRuntimePropertySynchronizationResult?
                    unsuccessfulResult =
                        results.FirstOrDefault(
                            result =>
                                !result.CacheUpdated);

                if (unsuccessfulResult is not null)
                {
                    throw new InvalidDataException(
                        $"Compact property identifier "
                        + $"0x{unsuccessfulResult.Mapping.CompactPropertyId:X2} "
                        + $"returned '{unsuccessfulResult.Status}' during "
                        + "endpoint synchronization.");
                }

                await _connectionOwner.ReplaceAsync(
                    candidate,
                    cancellationToken);

                candidate =
                    null;

                _runtimeEndpoint.UpdateConnectionStatus(
                    new EndpointConnectionStatus(
                        EndpointConnectionState.Ready,
                        _timeProvider.GetUtcNow(),
                        "Compact endpoint is ready."));
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                if (candidate is not null)
                {
                    await candidate.DisposeAsync();
                }

                throw;
            }
            catch (Exception exception)
            {
                if (candidate is not null)
                {
                    await candidate.DisposeAsync();
                }

                _runtimeEndpoint.UpdateConnectionStatus(
                    new EndpointConnectionStatus(
                        EndpointConnectionState.Faulted,
                        _timeProvider.GetUtcNow(),
                        exception.Message));

                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();

        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed =
                true;

            await _connectionOwner.DisposeAsync();

            _runtimeEndpoint.UpdateConnectionStatus(
                new EndpointConnectionStatus(
                    EndpointConnectionState.Disconnected,
                    _timeProvider.GetUtcNow(),
                    "Compact endpoint connection coordinator was disposed."));
        }
        finally
        {
            _gate.Release();
        }
    }
}