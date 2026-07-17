namespace Hase.Transport;

/// <summary>
/// Exposes transport exchange-trace notifications.
/// </summary>
/// <remarks>
/// This is an optional transport capability. A transport connection may
/// implement this interface in addition to <see cref="ITransportConnection"/>.
/// </remarks>
public interface ITransportExchangeTraceSource
{
    /// <summary>
    /// Subscribes an exchange-trace observer.
    /// </summary>
    void SubscribeTrace(
        ITransportExchangeTraceObserver observer);

    /// <summary>
    /// Removes an exchange-trace observer.
    /// </summary>
    void UnsubscribeTrace(
        ITransportExchangeTraceObserver observer);
}