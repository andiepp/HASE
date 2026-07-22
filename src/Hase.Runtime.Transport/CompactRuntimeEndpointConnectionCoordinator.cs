using Hase.CompactProtocol;
using Hase.Core.Domain.Endpoints;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;

namespace Hase.Runtime.Transport;

/// <summary>
/// Coordinates connection establishment, descriptor validation, property
/// synchronization, operation, compact-event authority, connection replacement,
/// faulted-connection detachment, and shutdown for one compact runtime endpoint.
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
    private readonly CompactMappedEventNotificationSource _eventSource;
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
            CreateEmptyEventSource(
                propertyMap),
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
        : this(
            connectionFactory,
            transportOptions,
            propertyMap,
            runtimeEndpoint,
            compatibilityValidator,
            connectionOwner,
            CreateEmptyEventSource(
                propertyMap),
            timeProvider)
    {
    }

    internal CompactRuntimeEndpointConnectionCoordinator(
        ICompactEndpointConnectionFactory connectionFactory,
        SerialTransportOptions transportOptions,
        CompactPropertyMap propertyMap,
        RuntimeEndpoint runtimeEndpoint,
        EndpointDescriptorCompatibilityValidator compatibilityValidator,
        CompactEndpointConnectionOwner connectionOwner,
        CompactMappedEventNotificationSource eventSource,
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

        _eventSource =
            eventSource
            ?? throw new ArgumentNullException(
                nameof(eventSource));

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

    internal CompactMappedEventNotificationSource EventSource =>
        _eventSource;

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

    /// <summary>
    /// Writes one compact endpoint property through the active connection and
    /// updates the runtime cache only from a successful confirmation read.
    /// </summary>
    public async Task<CompactRuntimePropertyWriteResult> WritePropertyAsync(
        byte compactPropertyId,
        object value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(
            cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);

            if (_runtimeEndpoint.ConnectionStatus.State
                != EndpointConnectionState.Ready)
            {
                throw new InvalidOperationException(
                    "Compact endpoint properties can be written only while "
                    + "the runtime endpoint is Ready.");
            }

            CompactEndpointConnection activeConnection =
                _connectionOwner.Current
                ?? throw new InvalidOperationException(
                    "The compact runtime endpoint does not have an active "
                    + "connection.");

            var propertyWriter =
                new CompactRuntimePropertyWriter(
                    activeConnection.Connection,
                    _propertyMap);

            return await propertyWriter.WriteAsync(
                _runtimeEndpoint,
                compactPropertyId,
                value,
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
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

        _eventSource.Deactivate();

        _runtimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Faulted,
                _timeProvider.GetUtcNow(),
                message.Trim()));
    }

    /// <summary>
    /// Detaches and disposes the invalid active connection after the runtime
    /// endpoint has entered the faulted state.
    /// </summary>
    /// <remarks>
    /// The stable runtime endpoint and all cached property values remain
    /// unchanged. The runtime endpoint remains faulted until reconnection
    /// begins.
    /// </remarks>
    public async Task DetachFaultedConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(
            cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);

            if (_runtimeEndpoint.ConnectionStatus.State
                != EndpointConnectionState.Faulted)
            {
                throw new InvalidOperationException(
                    "The active compact connection can be detached through "
                    + "the fault-recovery path only while the runtime endpoint "
                    + "is Faulted.");
            }

            _eventSource.Deactivate();

            await _connectionOwner.DetachAsync(
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
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

            // Revoke the previous connection before replacement work begins.
            // Compact events received while disconnected or recovering are
            // intentionally neither queued nor replayed.
            _eventSource.Deactivate();

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

                _runtimeEndpoint.UpdateConnectionStatus(
                    new EndpointConnectionStatus(
                        EndpointConnectionState.Ready,
                        _timeProvider.GetUtcNow(),
                        "Compact endpoint is ready."));

                // Event delivery begins only after operational validation,
                // synchronization, replacement, and the Ready transition.
                _eventSource.Activate(
                    candidate);

                candidate =
                    null;
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

                _eventSource.Deactivate();

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

            // Revoke event delivery before disposing the active physical
            // connection so a receive-loop race cannot publish after shutdown.
            _eventSource.Deactivate();

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

    private static CompactMappedEventNotificationSource
        CreateEmptyEventSource(
            CompactPropertyMap propertyMap)
    {
        ArgumentNullException.ThrowIfNull(
            propertyMap);

        var eventMap =
            new CompactEventMap(
                propertyMap.DescriptorDefinition,
                []);

        return new CompactMappedEventNotificationSource(
            new CompactEventNotificationResolver(
                eventMap));
    }
}