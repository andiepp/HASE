using Hase.Runtime.Connections;

namespace Hase.Runtime.Tests.Connections;

public sealed class EndpointConnectionStatusTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var timestamp = new DateTimeOffset(
            2026,
            7,
            10,
            12,
            0,
            0,
            TimeSpan.Zero);

        var status = new EndpointConnectionStatus(
            EndpointConnectionState.Ready,
            timestamp,
            "Synchronization completed.");

        Assert.Equal(
            EndpointConnectionState.Ready,
            status.State);

        Assert.Equal(
            timestamp,
            status.ChangedAtUtc);

        Assert.Equal(
            "Synchronization completed.",
            status.Detail);
    }

    [Fact]
    public void Constructor_WhitespaceDetail_NormalizesToNull()
    {
        var status = new EndpointConnectionStatus(
            EndpointConnectionState.Disconnected,
            detail: "   ");

        Assert.Null(status.Detail);
    }

    [Fact]
    public void Constructor_NonUtcTimestamp_Throws()
    {
        var timestamp = new DateTimeOffset(
            2026,
            7,
            10,
            12,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            () => new EndpointConnectionStatus(
                EndpointConnectionState.Ready,
                timestamp));
    }
}