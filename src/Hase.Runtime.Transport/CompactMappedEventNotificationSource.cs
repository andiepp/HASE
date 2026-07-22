using Hase.CompactProtocol;

namespace Hase.Runtime.Transport;

/// <summary>
/// Owns mapped compact-event delivery authority for exactly one accepted
/// operational compact endpoint connection at a time.
/// </summary>
internal sealed class CompactMappedEventNotificationSource
{
    private readonly CompactEventNotificationResolver _resolver;
    private readonly object _gate =
        new();

    private CompactEndpointConnection? _activeConnection;
    private Action<CompactEventNotification>? _activeHandler;

    public CompactMappedEventNotificationSource(
        CompactEventNotificationResolver resolver)
    {
        _resolver =
            resolver
            ?? throw new ArgumentNullException(
                nameof(resolver));
    }

    /// <summary>
    /// Occurs when the currently authoritative connection delivers one valid
    /// descriptor-mapped compact event notification.
    /// </summary>
    public event Action<CompactMappedEventNotification>?
        MappedEventNotificationReceived;

    /// <summary>
    /// Gets the connection currently authorized to deliver mapped events.
    /// </summary>
    internal CompactEndpointConnection? ActiveConnection
    {
        get
        {
            lock (_gate)
            {
                return _activeConnection;
            }
        }
    }

    /// <summary>
    /// Grants mapped-event delivery authority to one accepted operational
    /// connection and revokes any previous authority.
    /// </summary>
    public void Activate(
        CompactEndpointConnection connection)
    {
        ArgumentNullException.ThrowIfNull(
            connection);

        lock (_gate)
        {
            DeactivateCore();

            Action<CompactEventNotification> handler =
                notification =>
                    HandleNotification(
                        connection,
                        notification);

            _activeConnection =
                connection;

            _activeHandler =
                handler;

            connection.Connection.EventNotificationReceived +=
                handler;
        }
    }

    /// <summary>
    /// Revokes mapped-event delivery authority.
    /// </summary>
    public void Deactivate()
    {
        lock (_gate)
        {
            DeactivateCore();
        }
    }

    private void HandleNotification(
        CompactEndpointConnection sourceConnection,
        CompactEventNotification notification)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(
                    _activeConnection,
                    sourceConnection))
            {
                return;
            }

            CompactMappedEventNotification mappedNotification =
                _resolver.Resolve(
                    notification);

            MappedEventNotificationReceived?.Invoke(
                mappedNotification);
        }
    }

    private void DeactivateCore()
    {
        CompactEndpointConnection? activeConnection =
            _activeConnection;

        Action<CompactEventNotification>? activeHandler =
            _activeHandler;

        _activeConnection =
            null;

        _activeHandler =
            null;

        if (activeConnection is not null
            && activeHandler is not null)
        {
            activeConnection.Connection.EventNotificationReceived -=
                activeHandler;
        }
    }
}