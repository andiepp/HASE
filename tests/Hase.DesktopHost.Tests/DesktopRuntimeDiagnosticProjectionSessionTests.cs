using Hase.Runtime.Diagnostics;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeDiagnosticProjectionSessionTests
{
    [Fact]
    public void NewSession_ShouldRemainLocalOnly()
    {
        var session = new DesktopRuntimeDiagnosticSession();

        Publish(session, "Local");

        Assert.Null(session.ProjectionService);
        Assert.Equal("Local", Assert.Single(session.CaptureDiagnostics()).EventName);
    }

    [Fact]
    public async Task AttachProjection_ShouldPreservePublisherIdentity()
    {
        await using var session = new DesktopRuntimeDiagnosticSession();
        RuntimeDiagnosticPublisher publisher = session.Publisher;

        RuntimeHostDiagnosticProjectionService service = Attach(session);

        Assert.Same(publisher, session.Publisher);
        Assert.Same(service, session.ProjectionService);
    }

    [Fact]
    public async Task AttachProjection_ShouldEstablishLiveOnlyIdentityBoundary()
    {
        await using var session = new DesktopRuntimeDiagnosticSession();
        Publish(session, "Before");
        RuntimeHostDiagnosticProjectionService service = Attach(session, "host-authoritative");
        await using RuntimeHostDiagnosticProjectionSubscription subscription =
            await service.OpenSubscriptionAsync(
                new RuntimeHostDiagnosticProjectionSubscriptionOptions());

        Publish(session, "After");

        RuntimeHostProjectedDiagnosticObservation observation =
            await ReadOneAsync(subscription);
        Assert.Equal("After", observation.Record.EventName);
        Assert.Equal("host-authoritative", observation.Record.RuntimeHostId.Value);
        Assert.Equal(2, observation.Record.SourceSequence);
    }

    [Fact]
    public async Task AttachProjection_ShouldPreserveLocalRecordsBeforeAndAfter()
    {
        await using var session = new DesktopRuntimeDiagnosticSession();
        Publish(session, "Before");
        Attach(session);

        Publish(session, "After");

        Assert.Equal(
            ["Before", "After"],
            session.CaptureDiagnostics().Select(record => record.EventName).ToArray());
    }

    [Fact]
    public async Task AttachProjection_RemoteCeilingAboveLocal_ShouldRejectWithoutAttachment()
    {
        await using var session = new DesktopRuntimeDiagnosticSession(
            RuntimeDiagnosticLevel.Operational);

        Assert.Throws<ArgumentException>(() =>
            session.AttachProjection(
                new RuntimeHostId("host-one"),
                new RuntimeHostDiagnosticProjectionPolicy(
                    isEnabled: true,
                    maximumLevel: RuntimeDiagnosticLevel.Protocol)));
        Assert.Null(session.ProjectionService);
        Publish(session, "StillLocal");
        Assert.Single(session.CaptureDiagnostics());
    }

    [Fact]
    public async Task AttachProjection_RepeatedAttachment_ShouldReject()
    {
        await using var session = new DesktopRuntimeDiagnosticSession();
        Attach(session);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Attach(session));

        Assert.Equal("A diagnostic projection is already attached.", exception.Message);
    }

    [Fact]
    public async Task AttachProjection_ConcurrentPublication_ShouldLoseNoLocalRecords()
    {
        await using var session = new DesktopRuntimeDiagnosticSession(
            RuntimeDiagnosticLevel.Operational,
            capacity: 1200);
        Task publishing = Task.Run(() =>
        {
            for (int index = 0; index < 1000; index++)
            {
                Publish(session, $"Record{index}");
            }
        });

        Attach(session);
        await publishing;

        Assert.Equal(1000, session.CaptureDiagnostics().Count);
        Assert.Equal(
            Enumerable.Range(1, 1000).Select(value => (long)value),
            session.CaptureDiagnostics().Select(record => record.Sequence));
    }

    [Fact]
    public async Task DisposeAsync_ShouldEndAttachedSubscriptions()
    {
        var session = new DesktopRuntimeDiagnosticSession();
        RuntimeHostDiagnosticProjectionService service = Attach(session);
        await using RuntimeHostDiagnosticProjectionSubscription subscription =
            await service.OpenSubscriptionAsync(
                new RuntimeHostDiagnosticProjectionSubscriptionOptions());
        await using IAsyncEnumerator<RuntimeHostProjectedDiagnosticObservation> reader =
            subscription.ReadAllAsync().GetAsyncEnumerator();
        Task<bool> pendingRead = reader.MoveNextAsync().AsTask();

        await session.DisposeAsync();

        Assert.False(await pendingRead);
    }

    [Fact]
    public async Task DisposeAsync_ShouldBeIdempotentAndLeaveLocalCaptureUsable()
    {
        var session = new DesktopRuntimeDiagnosticSession();
        Attach(session);

        await session.DisposeAsync();
        await session.DisposeAsync();
        Publish(session, "AfterDispose");

        Assert.Equal(
            "AfterDispose",
            Assert.Single(session.CaptureDiagnostics()).EventName);
    }

    [Fact]
    public async Task AttachProjection_AfterDisposal_ShouldReject()
    {
        var session = new DesktopRuntimeDiagnosticSession();
        await session.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => Attach(session));
    }

    private static RuntimeHostDiagnosticProjectionService Attach(
        DesktopRuntimeDiagnosticSession session,
        string runtimeHostId = "host-one")
    {
        return session.AttachProjection(
            new RuntimeHostId(runtimeHostId),
            new RuntimeHostDiagnosticProjectionPolicy(isEnabled: true));
    }

    private static void Publish(
        DesktopRuntimeDiagnosticSession session,
        string eventName)
    {
        session.Publisher.Publish(new RuntimeDiagnosticEvent(
            RuntimeDiagnosticLevel.Operational,
            RuntimeDiagnosticCategory.RuntimeConnection,
            eventName));
    }

    private static async Task<RuntimeHostProjectedDiagnosticObservation> ReadOneAsync(
        RuntimeHostDiagnosticProjectionSubscription subscription)
    {
        await using IAsyncEnumerator<RuntimeHostProjectedDiagnosticObservation> reader =
            subscription.ReadAllAsync().GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        return reader.Current;
    }
}
