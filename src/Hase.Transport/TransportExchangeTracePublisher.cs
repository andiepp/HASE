namespace Hase.Transport;

/// <summary>
/// Provides thread-safe transport exchange-trace subscription and publication.
/// </summary>
internal sealed class TransportExchangeTracePublisher
{
    private readonly object _syncRoot =
        new();

    private readonly List<ITransportExchangeTraceObserver> _observers =
        [];

    public void Subscribe(
        ITransportExchangeTraceObserver observer)
    {
        ArgumentNullException.ThrowIfNull(
            observer);

        lock (_syncRoot)
        {
            bool alreadySubscribed =
                _observers.Any(
                    current =>
                        ReferenceEquals(
                            current,
                            observer));

            if (!alreadySubscribed)
            {
                _observers.Add(
                    observer);
            }
        }
    }

    public void Unsubscribe(
        ITransportExchangeTraceObserver observer)
    {
        ArgumentNullException.ThrowIfNull(
            observer);

        lock (_syncRoot)
        {
            _observers.RemoveAll(
                current =>
                    ReferenceEquals(
                        current,
                        observer));
        }
    }

    public void Publish(
        TransportExchangeTrace trace)
    {
        ArgumentNullException.ThrowIfNull(
            trace);

        ITransportExchangeTraceObserver[] observers;

        lock (_syncRoot)
        {
            observers =
                _observers.ToArray();
        }

        foreach (ITransportExchangeTraceObserver observer
                 in observers)
        {
            try
            {
                observer.OnTransportExchangeCompleted(
                    trace);
            }
            catch
            {
                // Trace observers are observational and must never change
                // transport behavior or prevent notification of later
                // observers.
            }
        }
    }
}