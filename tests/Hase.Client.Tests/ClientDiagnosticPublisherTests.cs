using Hase.Client.Diagnostics;

namespace Hase.Client.Tests;

public sealed class ClientDiagnosticPublisherTests
{
    [Fact]
    public void Publish_Enabled_AssignsUtcTimestampAndIncreasingSequence()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        ClientDiagnosticPublisher publisher = new(collector);

        DateTimeOffset before = DateTimeOffset.UtcNow;
        publisher.Publish(CreateEvent("First"));
        publisher.Publish(CreateEvent("Second"));
        DateTimeOffset after = DateTimeOffset.UtcNow;

        ClientDiagnosticSnapshot snapshot = collector.GetSnapshot();

        Assert.Equal(new long[] { 1, 2 }, snapshot.Records.Select(record => record.Sequence));
        Assert.All(snapshot.Records, record => Assert.Equal(TimeSpan.Zero, record.TimestampUtc.Offset));
        Assert.All(
            snapshot.Records,
            record => Assert.InRange(
                record.TimestampUtc,
                before,
                after));
    }

    [Fact]
    public void Publish_Disabled_DoesNotEvaluateFactory()
    {
        ClientDiagnosticPublisher publisher = new();
        bool evaluated = false;

        publisher.Publish(
            ClientDiagnosticLevel.Bytes,
            () =>
            {
                evaluated = true;
                return CreateEvent("Bytes");
            });

        Assert.False(evaluated);
    }

    [Fact]
    public void Publish_ThrowingSink_DoesNotAffectCaller()
    {
        ClientDiagnosticPublisher publisher = new(new ThrowingSink());

        Assert.Null(Record.Exception(() => publisher.Publish(CreateEvent("Connection"))));
        Assert.False(publisher.IsEnabled(ClientDiagnosticLevel.Operational));
    }

    private static ClientDiagnosticEvent CreateEvent(string name) =>
        new(
            ClientDiagnosticLevel.Operational,
            ClientDiagnosticCategory.ClientConnection,
            name);

    private sealed class ThrowingSink : IClientDiagnosticSink
    {
        public bool IsEnabled(ClientDiagnosticLevel level) =>
            throw new InvalidOperationException("Observer failure.");

        public void Publish(ClientDiagnosticRecord record) =>
            throw new InvalidOperationException("Observer failure.");
    }
}
