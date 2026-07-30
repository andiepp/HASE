using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Tests;

public sealed class RuntimeTransportByteDiagnosticPublisherTests
{
    [Fact]
    public void Publish_CompleteCapture_PublishesOwnedMetadataAndBytes()
    {
        byte[] source =
        [
            0x01,
            0x02,
            0x03
        ];

        BoundedRuntimeDiagnosticCollector collector =
            CreateBytesCollector();

        RuntimeTransportByteDiagnosticPublisher publisher =
            CreatePublisher(
                collector);

        publisher.Publish(
            RuntimeDiagnosticDirection.Outbound,
            "42",
            () =>
                source);

        source[0] =
            0xFF;

        RuntimeDiagnosticRecord record =
            Assert.Single(
                collector.GetSnapshot(
                    RuntimeDiagnosticLevel.Bytes));

        Assert.Equal(
            "TransportBytesSent",
            record.EventName);
        Assert.Equal(
            RuntimeDiagnosticCategory.TransportBytes,
            record.Category);
        Assert.Equal(
            RuntimeDiagnosticDirection.Outbound,
            record.Direction);
        Assert.Equal(
            "NativeProtocolV1",
            record.Details["protocolFamily"]);
        Assert.Equal(
            "42",
            record.Details["correlationId"]);
        Assert.Equal(
            "3",
            record.Details["originalByteCount"]);
        Assert.Equal(
            "3",
            record.Details["capturedByteCount"]);
        Assert.Equal(
            "False",
            record.Details["isTruncated"]);

        RuntimeDiagnosticByteSnapshot snapshot =
            Assert.IsType<RuntimeDiagnosticByteSnapshot>(
                record.ByteSnapshot);

        Assert.Equal(
            new byte[]
            {
                0x01,
                0x02,
                0x03
            },
            snapshot.ToArray());
    }

    [Fact]
    public void Publish_OversizedCapture_TruncatesAtFixedBound()
    {
        byte[] source =
            Enumerable
                .Range(
                    0,
                    RuntimeDiagnosticByteSnapshot
                        .MaximumCapturedByteCount
                    + 17)
                .Select(
                    value =>
                        (byte)value)
                .ToArray();

        BoundedRuntimeDiagnosticCollector collector =
            CreateBytesCollector();

        CreatePublisher(
                collector)
            .Publish(
                RuntimeDiagnosticDirection.Inbound,
                correlationId: null,
                () =>
                    source);

        RuntimeDiagnosticRecord record =
            Assert.Single(
                collector.GetSnapshot(
                    RuntimeDiagnosticLevel.Bytes));

        RuntimeDiagnosticByteSnapshot snapshot =
            Assert.IsType<RuntimeDiagnosticByteSnapshot>(
                record.ByteSnapshot);

        Assert.Equal(
            source.Length,
            snapshot.OriginalByteCount);
        Assert.Equal(
            RuntimeDiagnosticByteSnapshot.MaximumCapturedByteCount,
            snapshot.CapturedByteCount);
        Assert.True(
            snapshot.IsTruncated);
        Assert.Equal(
            "none",
            record.Details["correlationId"]);
        Assert.Equal(
            "TransportBytesReceived",
            record.EventName);
    }

    [Fact]
    public void Publish_ProtocolCollector_DoesNotEvaluateBytesFactory()
    {
        var collector =
            new BoundedRuntimeDiagnosticCollector(
                4,
                RuntimeDiagnosticLevel.Protocol);

        bool evaluated =
            false;

        CreatePublisher(
                collector)
            .Publish(
                RuntimeDiagnosticDirection.Outbound,
                "42",
                () =>
                {
                    evaluated =
                        true;

                    return new byte[]
                    {
                        0xA5
                    };
                });

        Assert.False(
            evaluated);
        Assert.Empty(
            collector.GetSnapshot());
    }

    [Fact]
    public void Publish_BytesCollector_IncludesCumulativeLowerLevels()
    {
        BoundedRuntimeDiagnosticCollector collector =
            CreateBytesCollector();

        Assert.True(
            collector.IsEnabled(
                RuntimeDiagnosticLevel.Operational));
        Assert.True(
            collector.IsEnabled(
                RuntimeDiagnosticLevel.Protocol));
        Assert.True(
            collector.IsEnabled(
                RuntimeDiagnosticLevel.Bytes));
    }

    [Theory]
    [InlineData(-1, 0, false)]
    [InlineData(0, 1, false)]
    [InlineData(2, 1, false)]
    [InlineData(1, 1, true)]
    public void ByteSnapshot_InconsistentMetadata_ShouldThrow(
        int originalByteCount,
        int capturedByteCount,
        bool isTruncated)
    {
        byte[] captured =
            new byte[
                Math.Max(
                    capturedByteCount,
                    0)];

        Assert.ThrowsAny<ArgumentException>(
            () =>
                new RuntimeDiagnosticByteSnapshot(
                    originalByteCount,
                    captured,
                    isTruncated));
    }

    [Fact]
    public void ByteSnapshot_ExceedingCaptureBound_ShouldThrow()
    {
        byte[] captured =
            new byte[
                RuntimeDiagnosticByteSnapshot
                    .MaximumCapturedByteCount
                + 1];

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new RuntimeDiagnosticByteSnapshot(
                    captured.Length,
                    captured,
                    isTruncated: false));
    }

    [Fact]
    public void Publish_InvalidDirection_ShouldThrowWithoutEvaluatingFactory()
    {
        bool evaluated =
            false;

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                CreatePublisher(
                        CreateBytesCollector())
                    .Publish(
                        (RuntimeDiagnosticDirection)99,
                        "42",
                        () =>
                        {
                            evaluated =
                                true;

                            return ReadOnlyMemory<byte>.Empty;
                        }));

        Assert.False(
            evaluated);
    }

    [Fact]
    public void Publish_ThrowingSink_DoesNotPropagate()
    {
        var publisher =
            new RuntimeTransportByteDiagnosticPublisher(
                new RuntimeDiagnosticPublisher(
                    new ThrowingSink()),
                "endpoint-one",
                "NativeProtocolV1");

        Exception? exception =
            Record.Exception(
                () =>
                    publisher.Publish(
                        RuntimeDiagnosticDirection.Outbound,
                        "42",
                        () =>
                            new byte[]
                            {
                                0xA5
                            }));

        Assert.Null(
            exception);
    }

    private static BoundedRuntimeDiagnosticCollector CreateBytesCollector()
    {
        return new BoundedRuntimeDiagnosticCollector(
            10,
            RuntimeDiagnosticLevel.Bytes);
    }

    private static RuntimeTransportByteDiagnosticPublisher CreatePublisher(
        IRuntimeDiagnosticSink sink)
    {
        return new RuntimeTransportByteDiagnosticPublisher(
            new RuntimeDiagnosticPublisher(
                sink),
            "endpoint-one",
            "NativeProtocolV1");
    }

    private sealed class ThrowingSink
        : IRuntimeDiagnosticSink
    {
        public bool IsEnabled(
            RuntimeDiagnosticLevel level)
        {
            return true;
        }

        public void Publish(
            RuntimeDiagnosticRecord record)
        {
            throw new InvalidOperationException(
                "sink failure");
        }
    }
}
