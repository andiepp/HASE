using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RuntimeHostClientSessionStatusChangedEventArgsTests
{
    [Fact]
    public void Constructor_ShouldPreserveTransition()
    {
        var previous =
            new RuntimeHostClientSessionStatus(
                RuntimeHostClientSessionState.Disconnected);
        var current =
            new RuntimeHostClientSessionStatus(
                RuntimeHostClientSessionState.Connecting);

        var eventArgs =
            new RuntimeHostClientSessionStatusChangedEventArgs(
                previous,
                current);

        Assert.Same(
            previous,
            eventArgs.Previous);
        Assert.Same(
            current,
            eventArgs.Current);
    }

    [Fact]
    public void Constructor_NullPrevious_ShouldThrow()
    {
        var current =
            new RuntimeHostClientSessionStatus(
                RuntimeHostClientSessionState.Connecting);

        Assert.Throws<ArgumentNullException>(
            "previous",
            () =>
                new RuntimeHostClientSessionStatusChangedEventArgs(
                    null!,
                    current));
    }

    [Fact]
    public void Constructor_NullCurrent_ShouldThrow()
    {
        var previous =
            new RuntimeHostClientSessionStatus(
                RuntimeHostClientSessionState.Disconnected);

        Assert.Throws<ArgumentNullException>(
            "current",
            () =>
                new RuntimeHostClientSessionStatusChangedEventArgs(
                    previous,
                    null!));
    }

    [Fact]
    public void Constructor_EqualStatuses_ShouldThrow()
    {
        var status =
            new RuntimeHostClientSessionStatus(
                RuntimeHostClientSessionState.Disconnected);

        Assert.Throws<ArgumentException>(
            "current",
            () =>
                new RuntimeHostClientSessionStatusChangedEventArgs(
                    status,
                    status));
    }
}
