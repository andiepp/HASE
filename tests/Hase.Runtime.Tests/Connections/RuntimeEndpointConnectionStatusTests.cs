using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Tests.Connections;

public sealed class RuntimeEndpointConnectionStatusTests
{
    private static readonly DateTimeOffset TestTimestamp =
        new(
            2026,
            7,
            10,
            12,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Constructor_InitializesDisconnectedStatus()
    {
        var endpoint = CreateEndpoint();

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            endpoint.ConnectionStatus.State);

        Assert.Null(
            endpoint.ConnectionStatus.ChangedAtUtc);
    }

    [Fact]
    public void UpdateConnectionStatus_UpdatesCurrentStatus()
    {
        var endpoint = CreateEndpoint();

        var status = new EndpointConnectionStatus(
            EndpointConnectionState.Connecting,
            TestTimestamp);

        endpoint.UpdateConnectionStatus(status);

        Assert.Same(status, endpoint.ConnectionStatus);
    }

    [Fact]
    public void UpdateConnectionStatus_NotifiesObserver()
    {
        var endpoint = CreateEndpoint();
        var observer = new RecordingObserver();

        endpoint.SubscribeConnectionStatus(observer);

        var status = new EndpointConnectionStatus(
            EndpointConnectionState.Connecting,
            TestTimestamp);

        endpoint.UpdateConnectionStatus(status);

        var change = Assert.Single(observer.Changes);

        Assert.Same(endpoint, change.Endpoint);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            change.PreviousStatus.State);

        Assert.Same(status, change.CurrentStatus);
    }

    [Fact]
    public void UpdateConnectionStatus_EqualStatus_DoesNotNotify()
    {
        var endpoint = CreateEndpoint();
        var observer = new RecordingObserver();

        endpoint.SubscribeConnectionStatus(observer);

        var status = new EndpointConnectionStatus(
            EndpointConnectionState.Connecting,
            TestTimestamp);

        endpoint.UpdateConnectionStatus(status);
        endpoint.UpdateConnectionStatus(status);

        Assert.Single(observer.Changes);
    }

    [Fact]
    public void SubscribeConnectionStatus_SameObserverTwice_NotifiesOnce()
    {
        var endpoint = CreateEndpoint();
        var observer = new RecordingObserver();

        endpoint.SubscribeConnectionStatus(observer);
        endpoint.SubscribeConnectionStatus(observer);

        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Connecting,
                TestTimestamp));

        Assert.Single(observer.Changes);
    }

    [Fact]
    public void UnsubscribeConnectionStatus_StopsNotifications()
    {
        var endpoint = CreateEndpoint();
        var observer = new RecordingObserver();

        endpoint.SubscribeConnectionStatus(observer);
        endpoint.UnsubscribeConnectionStatus(observer);

        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Connecting,
                TestTimestamp));

        Assert.Empty(observer.Changes);
    }

    [Fact]
    public void UpdateConnectionStatus_NullStatus_Throws()
    {
        var endpoint = CreateEndpoint();

        Assert.Throws<ArgumentNullException>(
            () => endpoint.UpdateConnectionStatus(null!));
    }

    private static RuntimeEndpoint CreateEndpoint()
    {
        var context = new RuntimeContext();

        return context.AddEndpoint(
            new EndpointDescriptor(
                new EndpointId("test-endpoint")));
    }

    private sealed class RecordingObserver
        : IEndpointConnectionStatusObserver
    {
        public List<EndpointConnectionStatusChanged>
            Changes
        { get; } = [];

        public void OnEndpointConnectionStatusChanged(
            EndpointConnectionStatusChanged change)
        {
            Changes.Add(change);
        }
    }
}