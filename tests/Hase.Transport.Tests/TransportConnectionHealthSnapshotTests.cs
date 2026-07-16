namespace Hase.Transport.Tests;

public sealed class TransportConnectionHealthSnapshotTests
{
    [Fact]
    public void Constructor_WithoutConnection_ShouldCreateSnapshot()
    {
        var snapshot =
            new TransportConnectionHealthSnapshot(
                hasConnection: false,
                state: null,
                lastStateChangeUtc: null,
                replacementCount: 0);

        Assert.False(snapshot.HasConnection);
        Assert.Null(snapshot.State);
        Assert.Null(snapshot.LastStateChangeUtc);
        Assert.Equal(0, snapshot.ReplacementCount);
    }

    [Fact]
    public void Constructor_WithConnection_ShouldCreateSnapshot()
    {
        DateTimeOffset timestamp =
            DateTimeOffset.UtcNow;

        var snapshot =
            new TransportConnectionHealthSnapshot(
                hasConnection: true,
                state: TransportConnectionState.Connected,
                lastStateChangeUtc: timestamp,
                replacementCount: 3);

        Assert.True(snapshot.HasConnection);
        Assert.Equal(
            TransportConnectionState.Connected,
            snapshot.State);
        Assert.Equal(
            timestamp,
            snapshot.LastStateChangeUtc);
        Assert.Equal(
            3,
            snapshot.ReplacementCount);
    }

    [Fact]
    public void Constructor_HasConnectionWithoutState_ShouldThrow()
    {
        void Act()
        {
            _ =
                new TransportConnectionHealthSnapshot(
                    hasConnection: true,
                    state: null,
                    lastStateChangeUtc: null,
                    replacementCount: 0);
        }

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                Act);

        Assert.Equal(
            "state",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_NoConnectionWithState_ShouldThrow()
    {
        void Act()
        {
            _ =
                new TransportConnectionHealthSnapshot(
                    hasConnection: false,
                    state: TransportConnectionState.Connected,
                    lastStateChangeUtc: null,
                    replacementCount: 0);
        }

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                Act);

        Assert.Equal(
            "state",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_NegativeReplacementCount_ShouldThrow()
    {
        void Act()
        {
            _ =
                new TransportConnectionHealthSnapshot(
                    hasConnection: false,
                    state: null,
                    lastStateChangeUtc: null,
                    replacementCount: -1);
        }

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                Act);

        Assert.Equal(
            "replacementCount",
            exception.ParamName);
    }

    [Fact]
    public void RecordEquality_ShouldCompareByValue()
    {
        DateTimeOffset timestamp =
            DateTimeOffset.UtcNow;

        var first =
            new TransportConnectionHealthSnapshot(
                false,
                null,
                timestamp,
                2);

        var second =
            new TransportConnectionHealthSnapshot(
                false,
                null,
                timestamp,
                2);

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void DifferentReplacementCount_ShouldNotBeEqual()
    {
        var first =
            new TransportConnectionHealthSnapshot(
                false,
                null,
                null,
                0);

        var second =
            new TransportConnectionHealthSnapshot(
                false,
                null,
                null,
                1);

        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void DifferentState_ShouldNotBeEqual()
    {
        DateTimeOffset timestamp =
            DateTimeOffset.UtcNow;

        var first =
            new TransportConnectionHealthSnapshot(
                true,
                TransportConnectionState.Connected,
                timestamp,
                0);

        var second =
            new TransportConnectionHealthSnapshot(
                true,
                TransportConnectionState.Faulted,
                timestamp,
                0);

        Assert.NotEqual(
            first,
            second);
    }
}