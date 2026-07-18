namespace Hase.Runtime.Transport;

/// <summary>
/// Provides subscriptions to unsolicited protocol notifications received by
/// a runtime protocol connection.
/// </summary>
public interface IRuntimeProtocolNotificationSource
{
    /// <summary>
    /// Subscribes an observer to protocol notifications.
    /// </summary>
    /// <param name="observer">
    /// Observer that receives decoded protocol notifications.
    /// </param>
    void SubscribeNotification(
        IProtocolNotificationObserver observer);

    /// <summary>
    /// Removes a protocol-notification observer.
    /// </summary>
    /// <param name="observer">
    /// Previously subscribed observer.
    /// </param>
    void UnsubscribeNotification(
        IProtocolNotificationObserver observer);
}