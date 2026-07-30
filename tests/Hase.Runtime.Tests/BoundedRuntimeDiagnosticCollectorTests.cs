using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Tests;

public sealed class BoundedRuntimeDiagnosticCollectorTests
{
    [Fact]
    public void IsEnabled_UsesCumulativeMaximumLevel()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                10,
                RuntimeDiagnosticLevel.Protocol);

        Assert.True(
            collector.IsEnabled(
                RuntimeDiagnosticLevel.Operational));

        Assert.True(
            collector.IsEnabled(
                RuntimeDiagnosticLevel.Protocol));

        Assert.False(
            collector.IsEnabled(
                RuntimeDiagnosticLevel.Bytes));
    }

    [Fact]
    public void Publish_OverCapacity_RetainsNewestRecordsInSequenceOrder()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                2);

        RuntimeDiagnosticPublisher publisher =
            new(
                collector);

        publisher.Publish(
            CreateEvent(
                "First"));

        publisher.Publish(
            CreateEvent(
                "Second"));

        publisher.Publish(
            CreateEvent(
                "Third"));

        IReadOnlyList<RuntimeDiagnosticRecord> snapshot =
            collector.GetSnapshot();

        Assert.Collection(
            snapshot,
            record =>
                Assert.Equal(
                    "Second",
                    record.EventName),
            record =>
                Assert.Equal(
                    "Third",
                    record.EventName));

        Assert.True(
            snapshot[0].Sequence <
            snapshot[1].Sequence);
    }

    [Fact]
    public void GetSnapshot_LevelAndCategory_ReturnsMatchingRecordsOnly()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                10,
                RuntimeDiagnosticLevel.Protocol);

        RuntimeDiagnosticPublisher publisher =
            new(
                collector);

        publisher.Publish(
            CreateEvent(
                "Connection"));

        publisher.Publish(
            new RuntimeDiagnosticEvent(
                RuntimeDiagnosticLevel.Protocol,
                RuntimeDiagnosticCategory.ProtocolExchange,
                "Request"));

        IReadOnlyList<RuntimeDiagnosticRecord> snapshot =
            collector.GetSnapshot(
                RuntimeDiagnosticLevel.Protocol,
                RuntimeDiagnosticCategory.ProtocolExchange);

        RuntimeDiagnosticRecord record =
            Assert.Single(
                snapshot);

        Assert.Equal(
            "Request",
            record.EventName);
    }

    [Fact]
    public void Clear_RemovesAllRecords()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                10);

        RuntimeDiagnosticPublisher publisher =
            new(
                collector);

        publisher.Publish(
            CreateEvent(
                "Connection"));

        collector.Clear();

        Assert.Empty(
            collector.GetSnapshot());
    }

    private static RuntimeDiagnosticEvent CreateEvent(
        string eventName)
    {
        return new RuntimeDiagnosticEvent(
            RuntimeDiagnosticLevel.Operational,
            RuntimeDiagnosticCategory.RuntimeConnection,
            eventName);
    }
}
