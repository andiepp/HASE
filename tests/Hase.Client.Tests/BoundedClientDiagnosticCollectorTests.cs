using Hase.Client.Diagnostics;

namespace Hase.Client.Tests;

public sealed class BoundedClientDiagnosticCollectorTests
{
    [Fact]
    public void IsEnabled_UsesCumulativeMaximumLevel()
    {
        BoundedClientDiagnosticCollector collector =
            new(10, ClientDiagnosticLevel.Protocol);

        Assert.True(collector.IsEnabled(ClientDiagnosticLevel.Operational));
        Assert.True(collector.IsEnabled(ClientDiagnosticLevel.Protocol));
        Assert.False(collector.IsEnabled(ClientDiagnosticLevel.Bytes));
    }

    [Fact]
    public void Publish_OverCapacity_RetainsNewestAndCountsEvictions()
    {
        BoundedClientDiagnosticCollector collector = new(2);
        ClientDiagnosticPublisher publisher = new(collector);

        publisher.Publish(CreateEvent("First"));
        publisher.Publish(CreateEvent("Second"));
        publisher.Publish(CreateEvent("Third"));

        ClientDiagnosticSnapshot snapshot = collector.GetSnapshot();

        Assert.Equal(
            new[] { "Second", "Third" },
            snapshot.Records.Select(record => record.EventName));
        Assert.Equal(1, snapshot.EvictedRecordCount);
    }

    [Fact]
    public void GetSnapshot_FiltersWithoutDiscardingRetainedRecords()
    {
        BoundedClientDiagnosticCollector collector =
            new(10, ClientDiagnosticLevel.Protocol);
        ClientDiagnosticPublisher publisher = new(collector);

        publisher.Publish(CreateEvent("Connection"));
        publisher.Publish(
            new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Protocol,
                ClientDiagnosticCategory.NorthboundExchange,
                "Request"));

        ClientDiagnosticSnapshot filtered = collector.GetSnapshot(
            ClientDiagnosticLevel.Protocol,
            ClientDiagnosticCategory.NorthboundExchange);

        Assert.Equal("Request", Assert.Single(filtered.Records).EventName);
        Assert.Equal(2, collector.GetSnapshot().Records.Count);
    }

    [Fact]
    public void Clear_RemovesRecordsAndResetsEvictionCount()
    {
        BoundedClientDiagnosticCollector collector = new(1);
        ClientDiagnosticPublisher publisher = new(collector);
        publisher.Publish(CreateEvent("First"));
        publisher.Publish(CreateEvent("Second"));

        collector.Clear();

        ClientDiagnosticSnapshot snapshot = collector.GetSnapshot();
        Assert.Empty(snapshot.Records);
        Assert.Equal(0, snapshot.EvictedRecordCount);
    }

    [Fact]
    public async Task ConcurrentPublication_RetainsNewestSequencesInOrder()
    {
        const int count = 500;
        const int capacity = 75;
        BoundedClientDiagnosticCollector collector = new(capacity);
        ClientDiagnosticPublisher publisher = new(collector);

        await Task.WhenAll(
            Enumerable.Range(0, count)
                .Select(index => Task.Run(
                    () => publisher.Publish(CreateEvent($"Event{index}")))));

        ClientDiagnosticSnapshot snapshot = collector.GetSnapshot();
        long[] sequences = snapshot.Records.Select(record => record.Sequence).ToArray();

        Assert.Equal(capacity, sequences.Length);
        Assert.Equal(count - capacity, snapshot.EvictedRecordCount);
        Assert.Equal(sequences.OrderBy(sequence => sequence), sequences);
        Assert.Equal(Enumerable.Range(count - capacity + 1, capacity).Select(value => (long)value), sequences);
    }

    private static ClientDiagnosticEvent CreateEvent(string name) =>
        new(
            ClientDiagnosticLevel.Operational,
            ClientDiagnosticCategory.ClientConnection,
            name);
}
