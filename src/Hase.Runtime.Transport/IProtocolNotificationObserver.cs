using Hase.Protocol;

namespace Hase.Runtime.Transport;

/// <summary>
/// Observes protocol notification messages received through a duplex protocol
/// session.
/// </summary>
/// <remarks>
/// Only messages with the Notification role and correlation identifier zero
/// are published to this observer.
///
/// Observer implementations are observational. Exceptions raised by an
/// observer must not terminate the protocol receive pump or prevent delivery
/// to other observers.
/// </remarks>
public interface IProtocolNotificationObserver
{
    /// <summary>
    /// Receives one decoded protocol notification.
    /// </summary>
    /// <param name="notification">
    /// The decoded notification message.
    /// </param>
    void OnProtocolNotification(
        ProtocolMessage notification);
}