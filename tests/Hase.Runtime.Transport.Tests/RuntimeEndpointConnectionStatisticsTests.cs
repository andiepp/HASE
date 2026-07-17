using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionStatisticsTests
{
    [Fact]
    public void Empty_ShouldContainZeroCountsAndNoRecoveryTimes()
    {
        RuntimeEndpointConnectionStatistics statistics =
            RuntimeEndpointConnectionStatistics.Empty;

        Assert.Equal(
            0,
            statistics.InitialConnectionAttemptCount);

        Assert.Equal(
            0,
            statistics.InitialConnectionFailureCount);

        Assert.Equal(
            0,
            statistics.ReconnectAttemptCount);

        Assert.Equal(
            0,
            statistics.ReconnectFailureCount);

        Assert.Equal(
            0,
            statistics.SuccessfulRecoveryCount);

        Assert.Null(
            statistics.LastRecoveryStartedAtUtc);

        Assert.Null(
            statistics.LastRecoveryCompletedAtUtc);

        Assert.Null(
            statistics.LastRecoveryDuration);
    }

    [Fact]
    public void Constructor_ShouldPreserveValues()
    {
        DateTimeOffset startedAtUtc =
            DateTimeOffset.FromUnixTimeMilliseconds(
                1_750_000_000_000);

        DateTimeOffset completedAtUtc =
            startedAtUtc.AddSeconds(
                4);

        TimeSpan duration =
            completedAtUtc - startedAtUtc;

        var statistics =
            new RuntimeEndpointConnectionStatistics(
                initialConnectionAttemptCount:
                    3,
                initialConnectionFailureCount:
                    2,
                reconnectAttemptCount:
                    5,
                reconnectFailureCount:
                    3,
                successfulRecoveryCount:
                    2,
                lastRecoveryStartedAtUtc:
                    startedAtUtc,
                lastRecoveryCompletedAtUtc:
                    completedAtUtc,
                lastRecoveryDuration:
                    duration);

        Assert.Equal(
            3,
            statistics.InitialConnectionAttemptCount);

        Assert.Equal(
            2,
            statistics.InitialConnectionFailureCount);

        Assert.Equal(
            5,
            statistics.ReconnectAttemptCount);

        Assert.Equal(
            3,
            statistics.ReconnectFailureCount);

        Assert.Equal(
            2,
            statistics.SuccessfulRecoveryCount);

        Assert.Equal(
            startedAtUtc,
            statistics.LastRecoveryStartedAtUtc);

        Assert.Equal(
            completedAtUtc,
            statistics.LastRecoveryCompletedAtUtc);

        Assert.Equal(
            duration,
            statistics.LastRecoveryDuration);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, 0)]
    [InlineData(0, -1, 0, 0, 0)]
    [InlineData(0, 0, -1, 0, 0)]
    [InlineData(0, 0, 0, -1, 0)]
    [InlineData(0, 0, 0, 0, -1)]
    public void Constructor_NegativeCount_ShouldThrow(
        long initialConnectionAttemptCount,
        long initialConnectionFailureCount,
        long reconnectAttemptCount,
        long reconnectFailureCount,
        long successfulRecoveryCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new RuntimeEndpointConnectionStatistics(
                    initialConnectionAttemptCount,
                    initialConnectionFailureCount,
                    reconnectAttemptCount,
                    reconnectFailureCount,
                    successfulRecoveryCount));
    }

    [Fact]
    public void Constructor_NonUtcStartTime_ShouldThrow()
    {
        var timestamp =
            new DateTimeOffset(
                2026,
                7,
                17,
                12,
                0,
                0,
                TimeSpan.FromHours(
                    2));

        Assert.Throws<ArgumentException>(
            () =>
                new RuntimeEndpointConnectionStatistics(
                    0,
                    0,
                    0,
                    0,
                    0,
                    lastRecoveryStartedAtUtc:
                        timestamp));
    }

    [Fact]
    public void Constructor_NonUtcCompletionTime_ShouldThrow()
    {
        var timestamp =
            new DateTimeOffset(
                2026,
                7,
                17,
                12,
                0,
                0,
                TimeSpan.FromHours(
                    2));

        Assert.Throws<ArgumentException>(
            () =>
                new RuntimeEndpointConnectionStatistics(
                    0,
                    0,
                    0,
                    0,
                    0,
                    lastRecoveryCompletedAtUtc:
                        timestamp));
    }

    [Fact]
    public void Constructor_NegativeRecoveryDuration_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new RuntimeEndpointConnectionStatistics(
                    0,
                    0,
                    0,
                    0,
                    0,
                    lastRecoveryDuration:
                        TimeSpan.FromMilliseconds(
                            -1)));
    }
}