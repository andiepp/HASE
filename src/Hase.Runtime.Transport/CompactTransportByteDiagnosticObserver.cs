using Hase.Runtime.Diagnostics;
using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Publishes bounded exact-byte diagnostics for Compact Serial Protocol V1
/// frames.
/// </summary>
internal sealed class CompactTransportByteDiagnosticObserver
    : ITransportByteTraceObserver
{
    private readonly RuntimeTransportByteDiagnosticPublisher diagnostics;

    public CompactTransportByteDiagnosticObserver(
        string endpointId,
        RuntimeDiagnosticPublisher diagnostics)
    {
        this.diagnostics =
            new RuntimeTransportByteDiagnosticPublisher(
                diagnostics,
                endpointId,
                "CompactSerialProtocolV1");
    }

    public void OnTransportBytes(
        TransportByteTrace trace)
    {
        diagnostics.Publish(
            trace.Direction switch
            {
                TransportByteDirection.Outbound =>
                    RuntimeDiagnosticDirection.Outbound,

                TransportByteDirection.Inbound =>
                    RuntimeDiagnosticDirection.Inbound,

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(trace),
                        trace.Direction,
                        "Direction is not defined.")
            },
            trace.CorrelationId,
            () =>
                trace.Bytes);
    }
}
