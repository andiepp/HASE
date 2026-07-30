using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Tests;

public sealed class RuntimeDiagnosticPublisherTests
{
    [Fact]
    public void Publish_Enabled_AssignsUtcTimestampAndIncreasingSequence()
    {
        DateTimeOffset localTimestamp =
            new(
                2026,
                7,
                30,
                13,
                0,
                0,
                TimeSpan.FromHours(2));

        BoundedRuntimeDiagnosticCollector collector =
            new(
                10);

        RuntimeDiagnosticPublisher publisher =
            new(
                collector,
                () => localTimestamp);

        publisher.Publish(
            CreateEvent(
                "First"));

        publisher.Publish(
            CreateEvent(
                "Second"));

        IReadOnlyList<RuntimeDiagnosticRecord> records =
            collector.GetSnapshot();

        Assert.Equal(
            1,
            records[0].Sequence);

        Assert.Equal(
            2,
            records[1].Sequence);

        Assert.Equal(
            TimeSpan.Zero,
            records[0].TimestampUtc.Offset);

        Assert.Equal(
            localTimestamp.ToUniversalTime(),
            records[0].TimestampUtc);
    }

    [Fact]
    public void Publish_Disabled_DoesNotEvaluateFactory()
    {
        RuntimeDiagnosticPublisher publisher =
            new();

        bool evaluated =
            false;

        publisher.Publish(
            RuntimeDiagnosticLevel.Bytes,
            () =>
            {
                evaluated = true;

                return new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Bytes,
                    RuntimeDiagnosticCategory.TransportBytes,
                    "BytesSent");
            });

        Assert.False(
            evaluated);
    }

    [Fact]
    public void Publish_ThrowingObserver_DoesNotPropagate()
    {
        RuntimeDiagnosticPublisher publisher =
            new(
                new ThrowingSink());

        Exception? exception =
            Record.Exception(
                () => publisher.Publish(
                    CreateEvent(
                        "Connection")));

        Assert.Null(
            exception);
    }

    [Fact]
    public void IsEnabled_ThrowingObserver_ReturnsFalse()
    {
        RuntimeDiagnosticPublisher publisher =
            new(
                new ThrowingSink(
                    throwFromIsEnabled: true));

        Assert.False(
            publisher.IsEnabled(
                RuntimeDiagnosticLevel.Operational));
    }

    private static RuntimeDiagnosticEvent CreateEvent(
        string eventName)
    {
        return new RuntimeDiagnosticEvent(
            RuntimeDiagnosticLevel.Operational,
            RuntimeDiagnosticCategory.RuntimeConnection,
            eventName);
    }

    private sealed class ThrowingSink :
        IRuntimeDiagnosticSink
    {
        private readonly bool throwFromIsEnabled;

        public ThrowingSink(
            bool throwFromIsEnabled = false)
        {
            this.throwFromIsEnabled =
                throwFromIsEnabled;
        }

        public bool IsEnabled(
            RuntimeDiagnosticLevel level)
        {
            if (throwFromIsEnabled)
            {
                throw new InvalidOperationException(
                    "Test observer failure.");
            }

            return true;
        }

        public void Publish(
            RuntimeDiagnosticRecord record)
        {
            throw new InvalidOperationException(
                "Test observer failure.");
        }
    }
}
