namespace Hase.Runtime.Transport;

/// <summary>
/// Preserves protocol-notification subscriptions across protocol connection
/// replacement.
/// </summary>
internal sealed class RuntimeProtocolNotificationSubscriptions
{
    private readonly object _syncRoot =
        new();

    private readonly List<IProtocolNotificationObserver>
        _observers =
        [];

    private IRuntimeProtocolNotificationSource? _source;

    /// <summary>
    /// Subscribes an observer and attaches it to the current source, if one
    /// exists.
    /// </summary>
    public void Subscribe(
        IProtocolNotificationObserver observer)
    {
        ArgumentNullException.ThrowIfNull(
            observer);

        lock (_syncRoot)
        {
            if (_observers.Any(
                    current =>
                        ReferenceEquals(
                            current,
                            observer)))
            {
                return;
            }

            _observers.Add(
                observer);

            _source?.SubscribeNotification(
                observer);
        }
    }

    /// <summary>
    /// Removes an observer from the persistent subscription set and from the
    /// current source, if one exists.
    /// </summary>
    public void Unsubscribe(
        IProtocolNotificationObserver observer)
    {
        ArgumentNullException.ThrowIfNull(
            observer);

        lock (_syncRoot)
        {
            int removedCount =
                _observers.RemoveAll(
                    current =>
                        ReferenceEquals(
                            current,
                            observer));

            if (removedCount > 0)
            {
                _source?.UnsubscribeNotification(
                    observer);
            }
        }
    }

    /// <summary>
    /// Moves all persistent subscriptions to a notification source.
    /// </summary>
    public void Attach(
        IRuntimeProtocolNotificationSource source)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        lock (_syncRoot)
        {
            if (ReferenceEquals(
                    _source,
                    source))
            {
                return;
            }

            DetachCurrentSource();

            _source =
                source;

            foreach (IProtocolNotificationObserver observer
                     in _observers)
            {
                source.SubscribeNotification(
                    observer);
            }
        }
    }

    /// <summary>
    /// Removes all persistent subscriptions from the supplied source while
    /// retaining them for a future replacement source.
    /// </summary>
    public void Detach(
        IRuntimeProtocolNotificationSource source)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        lock (_syncRoot)
        {
            if (!ReferenceEquals(
                    _source,
                    source))
            {
                return;
            }

            DetachCurrentSource();
        }
    }

    private void DetachCurrentSource()
    {
        IRuntimeProtocolNotificationSource? source =
            _source;

        if (source is null)
        {
            return;
        }

        foreach (IProtocolNotificationObserver observer
                 in _observers)
        {
            source.UnsubscribeNotification(
                observer);
        }

        _source =
            null;
    }
}