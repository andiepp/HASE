namespace Hase.Transport.Tests;

public sealed class TransportConnectionHealthChangedEventArgsTests
{
    [Fact]
    public void Constructor_ShouldStoreSnapshots()
    {
        var previous =
            new TransportConnectionHealthSnapshot(
                false,
                null,
                null,
                0);

        var current =
            new TransportConnectionHealthSnapshot(
                true,
                TransportConnectionState.Connected,
                DateTimeOffset.UtcNow,
                0);

        var eventArgs =
            new TransportConnectionHealthChangedEventArgs(
                previous,
                current);

        Assert.Same(
            previous,
            eventArgs.PreviousHealth);

        Assert.Same(
            current,
            eventArgs.CurrentHealth);
    }

    [Fact]
    public void Constructor_NullPrevious_ShouldThrow()
    {
        var current =
            new TransportConnectionHealthSnapshot(
                false,
                null,
                null,
                0);

        void Act()
        {
            _ =
                new TransportConnectionHealthChangedEventArgs(
                    null!,
                    current);
        }

        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "previousHealth",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_NullCurrent_ShouldThrow()
    {
        var previous =
            new TransportConnectionHealthSnapshot(
                false,
                null,
                null,
                0);

        void Act()
        {
            _ =
                new TransportConnectionHealthChangedEventArgs(
                    previous,
                    null!);
        }

        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "currentHealth",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_EqualSnapshots_ShouldThrow()
    {
        var snapshot =
            new TransportConnectionHealthSnapshot(
                false,
                null,
                null,
                0);

        void Act()
        {
            _ =
                new TransportConnectionHealthChangedEventArgs(
                    snapshot,
                    snapshot);
        }

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                Act);

        Assert.Equal(
            "currentHealth",
            exception.ParamName);
    }
}