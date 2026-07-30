namespace Hase.Transport;

/// <summary>
/// Observes complete raw transport frames synchronously.
/// </summary>
public interface ITransportByteTraceObserver
{
    void OnTransportBytes(
        TransportByteTrace trace);
}
