using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemoteEndpointConnectionStatusTests
{
    [Fact]
    public void Constructor_Values_ShouldPreserveStatus()
    {
        DateTimeOffset changedAtUtc =
            new(
                2026,
                7,
                27,
                8,
                0,
                0,
                TimeSpan.Zero);

        var status =
            new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Ready,
                changedAtUtc,
                "  synchronized  ");

        Assert.Equal(
            RemoteEndpointConnectionState.Ready,
            status.State);
        Assert.Equal(
            changedAtUtc,
            status.ChangedAtUtc);
        Assert.Equal(
            "synchronized",
            status.Detail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_MissingDetail_ShouldNormalizeToNull(
        string? detail)
    {
        var status =
            new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Disconnected,
                detail:
                    detail);

        Assert.Null(
            status.Detail);
    }

    [Fact]
    public void Constructor_NonUtcTimestamp_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "changedAtUtc",
            () => new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Ready,
                new DateTimeOffset(
                    2026,
                    7,
                    27,
                    10,
                    0,
                    0,
                    TimeSpan.FromHours(
                        2))));
    }

    [Fact]
    public void Constructor_UnspecifiedState_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "state",
            () => new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Unspecified));
    }

    [Fact]
    public void Constructor_UndefinedState_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "state",
            () => new RemoteEndpointConnectionStatus(
                (RemoteEndpointConnectionState) 99));
    }
}
