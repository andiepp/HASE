using Hase.Runtime.Diagnostics;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactTransportByteDiagnosticObserverTests
{
    [Fact]
    public void EnabledBytes_PublishesBoundedCompactSnapshot()
    {
        var collector =
            new BoundedRuntimeDiagnosticCollector(
                4,
                RuntimeDiagnosticLevel.Bytes);

        var observer =
            new CompactTransportByteDiagnosticObserver(
                "endpoint-one",
                new RuntimeDiagnosticPublisher(
                    collector));

        byte[] bytes =
            new byte[
                RuntimeDiagnosticByteSnapshot
                    .MaximumCapturedByteCount
                + 7];

        observer.OnTransportBytes(
            new TransportByteTrace(
                TransportByteDirection.Inbound,
                bytes,
                correlationId: null));

        RuntimeDiagnosticRecord record =
            Assert.Single(
                collector.GetSnapshot(
                    RuntimeDiagnosticLevel.Bytes));

        Assert.Equal(
            "CompactSerialProtocolV1",
            record.Details["protocolFamily"]);
        Assert.Equal(
            "none",
            record.Details["correlationId"]);

        RuntimeDiagnosticByteSnapshot snapshot =
            Assert.IsType<RuntimeDiagnosticByteSnapshot>(
                record.ByteSnapshot);

        Assert.Equal(
            bytes.Length,
            snapshot.OriginalByteCount);
        Assert.Equal(
            RuntimeDiagnosticByteSnapshot.MaximumCapturedByteCount,
            snapshot.CapturedByteCount);
        Assert.True(
            snapshot.IsTruncated);
    }

    [Fact]
    public void ProtocolOnly_PublishesNothing()
    {
        var collector =
            new BoundedRuntimeDiagnosticCollector(
                4,
                RuntimeDiagnosticLevel.Protocol);

        var observer =
            new CompactTransportByteDiagnosticObserver(
                "endpoint-one",
                new RuntimeDiagnosticPublisher(
                    collector));

        observer.OnTransportBytes(
            new TransportByteTrace(
                TransportByteDirection.Outbound,
                new byte[]
                {
                    0xA5
                },
                "33"));

        Assert.Empty(
            collector.GetSnapshot());
    }
}
