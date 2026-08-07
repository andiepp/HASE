using Hase.Runtime.Diagnostics;
using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostDiagnosticProjectionServiceTests
{
    [Fact]
    public void SubscriptionOptions_DefaultCapacity_IsBounded()
    {
        var options = new RuntimeHostDiagnosticProjectionSubscriptionOptions();

        Assert.Equal(256, options.BufferCapacity);
    }

    [Fact]
    public void SubscriptionOptions_NonPositiveCapacity_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RuntimeHostDiagnosticProjectionSubscriptionOptions(0));
    }

    [Fact]
    public async Task Subscription_IsLiveOnly()
    {
        var local = new BoundedRuntimeDiagnosticCollector(4);
        await using var service = CreateService(local);
        var publisher = new RuntimeDiagnosticPublisher(service);
        Publish(publisher, "Before");
        await using RuntimeHostDiagnosticProjectionSubscription subscription =
            await OpenAsync(service);

        Publish(publisher, "After");

        RuntimeHostProjectedDiagnosticObservation observation =
            await ReadOneAsync(subscription);
        Assert.Equal("After", observation.Record.EventName);
        Assert.Equal(2, observation.Record.SourceSequence);
    }

    [Fact]
    public async Task Subscription_UsesLocalSequenceAndPreservesSourceSequence()
    {
        await using var service = CreateService();
        var publisher = new RuntimeDiagnosticPublisher(service);
        Publish(publisher, "Before");
        await using RuntimeHostDiagnosticProjectionSubscription subscription =
            await OpenAsync(service);
        Publish(publisher, "First");
        Publish(publisher, "Second");

        await using IAsyncEnumerator<RuntimeHostProjectedDiagnosticObservation> reader =
            subscription.ReadAllAsync().GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(1, reader.Current.Sequence.Value);
        Assert.Equal(2, reader.Current.Record.SourceSequence);
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(2, reader.Current.Sequence.Value);
        Assert.Equal(3, reader.Current.Record.SourceSequence);
    }

    [Fact]
    public async Task DisabledProjection_StillForwardsToLocalSink()
    {
        var local = new BoundedRuntimeDiagnosticCollector(4);
        await using var service = new RuntimeHostDiagnosticProjectionService(
            new RuntimeHostId("host-one"),
            local,
            RuntimeDiagnosticLevel.Bytes);
        await using RuntimeHostDiagnosticProjectionSubscription subscription =
            await OpenAsync(service);
        var publisher = new RuntimeDiagnosticPublisher(service);

        Publish(publisher, "LocalOnly");
        await service.DisposeAsync();

        Assert.Equal("LocalOnly", Assert.Single(local.GetSnapshot()).EventName);
        await using IAsyncEnumerator<RuntimeHostProjectedDiagnosticObservation> reader =
            subscription.ReadAllAsync().GetAsyncEnumerator();
        Assert.False(await reader.MoveNextAsync());
    }

    [Fact]
    public async Task ProjectionPolicy_FiltersRecordsAboveItsCeiling()
    {
        var local = new BoundedRuntimeDiagnosticCollector(
            4,
            RuntimeDiagnosticLevel.Bytes);
        await using var service = CreateService(local);
        await using RuntimeHostDiagnosticProjectionSubscription subscription =
            await OpenAsync(service);
        var publisher = new RuntimeDiagnosticPublisher(service);

        Publish(publisher, "Filtered", RuntimeDiagnosticLevel.Protocol);
        Publish(publisher, "Projected");

        RuntimeHostProjectedDiagnosticObservation observation =
            await ReadOneAsync(subscription);
        Assert.Equal("Projected", observation.Record.EventName);
        Assert.Equal(2, local.GetSnapshot().Count);
    }

    [Fact]
    public async Task SlowSubscription_EndsWithExplicitGap()
    {
        await using var service = CreateService();
        await using RuntimeHostDiagnosticProjectionSubscription subscription =
            await OpenAsync(service, 1);
        var publisher = new RuntimeDiagnosticPublisher(service);
        Publish(publisher, "First");
        Publish(publisher, "Overflow");

        await Assert.ThrowsAsync<RuntimeHostDiagnosticProjectionGapException>(
            async () =>
            {
                await foreach (RuntimeHostProjectedDiagnosticObservation _
                    in subscription.ReadAllAsync())
                {
                }
            });
    }

    [Fact]
    public async Task SlowSubscription_DoesNotAffectHealthySubscriptionOrLocalSink()
    {
        var local = new BoundedRuntimeDiagnosticCollector(4);
        await using var service = CreateService(local);
        await using RuntimeHostDiagnosticProjectionSubscription slow =
            await OpenAsync(service, 1);
        await using RuntimeHostDiagnosticProjectionSubscription healthy =
            await OpenAsync(service, 4);
        var publisher = new RuntimeDiagnosticPublisher(service);
        Publish(publisher, "First");
        Publish(publisher, "Second");

        await using IAsyncEnumerator<RuntimeHostProjectedDiagnosticObservation> reader =
            healthy.ReadAllAsync().GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal("First", reader.Current.Record.EventName);
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal("Second", reader.Current.Record.EventName);
        Assert.Equal(2, local.GetSnapshot().Count);
    }

    [Fact]
    public async Task ThrowingLocalSink_DoesNotAffectProjection()
    {
        await using var service = CreateService(new ThrowingSink());
        await using RuntimeHostDiagnosticProjectionSubscription subscription =
            await OpenAsync(service);
        var publisher = new RuntimeDiagnosticPublisher(service);

        Publish(publisher, "Projected");

        Assert.Equal(
            "Projected",
            (await ReadOneAsync(subscription)).Record.EventName);
    }

    [Fact]
    public async Task Enumeration_HonorsCancellation()
    {
        await using var service = CreateService();
        await using RuntimeHostDiagnosticProjectionSubscription subscription =
            await OpenAsync(service);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using IAsyncEnumerator<RuntimeHostProjectedDiagnosticObservation> reader =
            subscription.ReadAllAsync(cancellation.Token).GetAsyncEnumerator();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await reader.MoveNextAsync();
        });
    }

    [Fact]
    public async Task SubscriptionDisposal_IsIdempotentAndIndependent()
    {
        await using var service = CreateService();
        RuntimeHostDiagnosticProjectionSubscription ended =
            await OpenAsync(service);
        await using RuntimeHostDiagnosticProjectionSubscription active =
            await OpenAsync(service);
        await ended.DisposeAsync();
        await ended.DisposeAsync();
        var publisher = new RuntimeDiagnosticPublisher(service);

        Publish(publisher, "Active");

        Assert.Equal("Active", (await ReadOneAsync(active)).Record.EventName);
    }

    [Fact]
    public async Task ServiceDisposal_EndsSubscriptionsRejectsNewOnesAndKeepsLocalSinkAlive()
    {
        var local = new BoundedRuntimeDiagnosticCollector(4);
        var service = CreateService(local);
        await using RuntimeHostDiagnosticProjectionSubscription subscription =
            await OpenAsync(service);
        await service.DisposeAsync();
        await service.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            OpenAsync(service));
        var publisher = new RuntimeDiagnosticPublisher(service);
        Publish(publisher, "AfterDispose");

        Assert.Equal("AfterDispose", Assert.Single(local.GetSnapshot()).EventName);
        await using IAsyncEnumerator<RuntimeHostProjectedDiagnosticObservation> reader =
            subscription.ReadAllAsync().GetAsyncEnumerator();
        Assert.False(await reader.MoveNextAsync());
    }

    private static RuntimeHostDiagnosticProjectionService CreateService(
        IRuntimeDiagnosticSink? local = null)
    {
        return new RuntimeHostDiagnosticProjectionService(
            new RuntimeHostId("host-one"),
            local ?? new BoundedRuntimeDiagnosticCollector(8),
            RuntimeDiagnosticLevel.Bytes,
            new RuntimeHostDiagnosticProjectionPolicy(isEnabled: true));
    }

    private static Task<RuntimeHostDiagnosticProjectionSubscription> OpenAsync(
        RuntimeHostDiagnosticProjectionService service,
        int capacity = 8)
    {
        return service.OpenSubscriptionAsync(
            new RuntimeHostDiagnosticProjectionSubscriptionOptions(capacity));
    }

    private static async Task<RuntimeHostProjectedDiagnosticObservation> ReadOneAsync(
        RuntimeHostDiagnosticProjectionSubscription subscription)
    {
        await using IAsyncEnumerator<RuntimeHostProjectedDiagnosticObservation> reader =
            subscription.ReadAllAsync().GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        return reader.Current;
    }

    private static void Publish(
        RuntimeDiagnosticPublisher publisher,
        string eventName,
        RuntimeDiagnosticLevel level = RuntimeDiagnosticLevel.Operational)
    {
        publisher.Publish(new RuntimeDiagnosticEvent(
            level,
            RuntimeDiagnosticCategory.RuntimeConnection,
            eventName,
            RuntimeDiagnosticSeverity.Information));
    }

    private sealed class ThrowingSink : IRuntimeDiagnosticSink
    {
        public bool IsEnabled(RuntimeDiagnosticLevel level) => true;

        public void Publish(RuntimeDiagnosticRecord record)
        {
            throw new InvalidOperationException("Expected test failure.");
        }
    }
}
