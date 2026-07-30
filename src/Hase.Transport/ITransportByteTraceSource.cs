namespace Hase.Transport;

/// <summary>
/// Exposes optional complete-frame raw-byte trace notifications.
/// </summary>
public interface ITransportByteTraceSource
{
    void SubscribeByteTrace(
        ITransportByteTraceObserver observer);

    void UnsubscribeByteTrace(
        ITransportByteTraceObserver observer);
}
