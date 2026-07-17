namespace Hase.Transport.Tests;

public sealed class TransportExchangeStatisticsTests
{
    [Fact]
    public void Empty_ShouldContainZeroValues()
    {
        // Act
        TransportExchangeStatistics statistics =
            TransportExchangeStatistics.Empty;

        // Assert
        Assert.Equal(
            0,
            statistics.CompletedExchangeCount);

        Assert.Equal(
            0,
            statistics.SuccessfulExchangeCount);

        Assert.Equal(
            0,
            statistics.FailedExchangeCount);

        Assert.Equal(
            0,
            statistics.CancelledExchangeCount);

        Assert.Equal(
            0,
            statistics.TotalRequestByteCount);

        Assert.Equal(
            0,
            statistics.TotalResponseByteCount);

        Assert.Equal(
            TimeSpan.Zero,
            statistics.TotalDuration);

        Assert.Null(
            statistics.LastCompletedAtUtc);

        Assert.Null(
            statistics.LastOutcome);
    }

    [Fact]
    public void Constructor_ValidValues_ShouldPreserveValues()
    {
        // Arrange
        DateTimeOffset completedAtUtc =
            new(
                year:
                    2026,
                month:
                    7,
                day:
                    17,
                hour:
                    10,
                minute:
                    30,
                second:
                    0,
                offset:
                    TimeSpan.Zero);

        TimeSpan totalDuration =
            TimeSpan.FromMilliseconds(
                425);

        // Act
        var statistics =
            new TransportExchangeStatistics(
                completedExchangeCount:
                    6,
                successfulExchangeCount:
                    3,
                failedExchangeCount:
                    2,
                cancelledExchangeCount:
                    1,
                totalRequestByteCount:
                    480,
                totalResponseByteCount:
                    2048,
                totalDuration:
                    totalDuration,
                lastCompletedAtUtc:
                    completedAtUtc,
                lastOutcome:
                    TransportExchangeOutcome.Cancelled);

        // Assert
        Assert.Equal(
            6,
            statistics.CompletedExchangeCount);

        Assert.Equal(
            3,
            statistics.SuccessfulExchangeCount);

        Assert.Equal(
            2,
            statistics.FailedExchangeCount);

        Assert.Equal(
            1,
            statistics.CancelledExchangeCount);

        Assert.Equal(
            480,
            statistics.TotalRequestByteCount);

        Assert.Equal(
            2048,
            statistics.TotalResponseByteCount);

        Assert.Equal(
            totalDuration,
            statistics.TotalDuration);

        Assert.Equal(
            completedAtUtc,
            statistics.LastCompletedAtUtc);

        Assert.Equal(
            TransportExchangeOutcome.Cancelled,
            statistics.LastOutcome);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, "completedExchangeCount")]
    [InlineData(0, -1, 0, 0, "successfulExchangeCount")]
    [InlineData(0, 0, -1, 0, "failedExchangeCount")]
    [InlineData(0, 0, 0, -1, "cancelledExchangeCount")]
    public void Constructor_NegativeExchangeCount_ShouldThrow(
        long completedExchangeCount,
        long successfulExchangeCount,
        long failedExchangeCount,
        long cancelledExchangeCount,
        string expectedParameterName)
    {
        // Act
        void Act()
        {
            _ = new TransportExchangeStatistics(
                completedExchangeCount:
                    completedExchangeCount,
                successfulExchangeCount:
                    successfulExchangeCount,
                failedExchangeCount:
                    failedExchangeCount,
                cancelledExchangeCount:
                    cancelledExchangeCount,
                totalRequestByteCount:
                    0,
                totalResponseByteCount:
                    0,
                totalDuration:
                    TimeSpan.Zero);
        }

        // Assert
        ArgumentOutOfRangeException exception =
            Assert.Throws<
                ArgumentOutOfRangeException>(
                    Act);

        Assert.Equal(
            expectedParameterName,
            exception.ParamName);
    }

    [Theory]
    [InlineData(-1, 0, "totalRequestByteCount")]
    [InlineData(0, -1, "totalResponseByteCount")]
    public void Constructor_NegativeByteCount_ShouldThrow(
        long totalRequestByteCount,
        long totalResponseByteCount,
        string expectedParameterName)
    {
        // Act
        void Act()
        {
            _ = new TransportExchangeStatistics(
                completedExchangeCount:
                    0,
                successfulExchangeCount:
                    0,
                failedExchangeCount:
                    0,
                cancelledExchangeCount:
                    0,
                totalRequestByteCount:
                    totalRequestByteCount,
                totalResponseByteCount:
                    totalResponseByteCount,
                totalDuration:
                    TimeSpan.Zero);
        }

        // Assert
        ArgumentOutOfRangeException exception =
            Assert.Throws<
                ArgumentOutOfRangeException>(
                    Act);

        Assert.Equal(
            expectedParameterName,
            exception.ParamName);
    }

    [Fact]
    public void Constructor_NegativeTotalDuration_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new TransportExchangeStatistics(
                completedExchangeCount:
                    0,
                successfulExchangeCount:
                    0,
                failedExchangeCount:
                    0,
                cancelledExchangeCount:
                    0,
                totalRequestByteCount:
                    0,
                totalResponseByteCount:
                    0,
                totalDuration:
                    TimeSpan.FromTicks(
                        -1));
        }

        // Assert
        ArgumentOutOfRangeException exception =
            Assert.Throws<
                ArgumentOutOfRangeException>(
                    Act);

        Assert.Equal(
            "totalDuration",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_InconsistentOutcomeCounts_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new TransportExchangeStatistics(
                completedExchangeCount:
                    4,
                successfulExchangeCount:
                    2,
                failedExchangeCount:
                    1,
                cancelledExchangeCount:
                    0,
                totalRequestByteCount:
                    10,
                totalResponseByteCount:
                    20,
                totalDuration:
                    TimeSpan.Zero,
                lastCompletedAtUtc:
                    DateTimeOffset.UnixEpoch,
                lastOutcome:
                    TransportExchangeOutcome.Succeeded);
        }

        // Assert
        ArgumentException exception =
            Assert.Throws<
                ArgumentException>(
                    Act);

        Assert.Equal(
            "completedExchangeCount",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_OutcomeCountOverflow_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new TransportExchangeStatistics(
                completedExchangeCount:
                    long.MaxValue,
                successfulExchangeCount:
                    long.MaxValue,
                failedExchangeCount:
                    1,
                cancelledExchangeCount:
                    0,
                totalRequestByteCount:
                    0,
                totalResponseByteCount:
                    0,
                totalDuration:
                    TimeSpan.Zero,
                lastCompletedAtUtc:
                    DateTimeOffset.UnixEpoch,
                lastOutcome:
                    TransportExchangeOutcome.Succeeded);
        }

        // Assert
        ArgumentException exception =
            Assert.Throws<
                ArgumentException>(
                    Act);

        Assert.Equal(
            "successfulExchangeCount",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_NonUtcCompletionTimestamp_ShouldThrow()
    {
        // Arrange
        DateTimeOffset nonUtcTimestamp =
            new(
                year:
                    2026,
                month:
                    7,
                day:
                    17,
                hour:
                    10,
                minute:
                    30,
                second:
                    0,
                offset:
                    TimeSpan.FromHours(
                        2));

        // Act
        void Act()
        {
            _ = new TransportExchangeStatistics(
                completedExchangeCount:
                    1,
                successfulExchangeCount:
                    1,
                failedExchangeCount:
                    0,
                cancelledExchangeCount:
                    0,
                totalRequestByteCount:
                    10,
                totalResponseByteCount:
                    20,
                totalDuration:
                    TimeSpan.Zero,
                lastCompletedAtUtc:
                    nonUtcTimestamp,
                lastOutcome:
                    TransportExchangeOutcome.Succeeded);
        }

        // Assert
        ArgumentException exception =
            Assert.Throws<
                ArgumentException>(
                    Act);

        Assert.Equal(
            "lastCompletedAtUtc",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_UndefinedLastOutcome_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new TransportExchangeStatistics(
                completedExchangeCount:
                    1,
                successfulExchangeCount:
                    1,
                failedExchangeCount:
                    0,
                cancelledExchangeCount:
                    0,
                totalRequestByteCount:
                    10,
                totalResponseByteCount:
                    20,
                totalDuration:
                    TimeSpan.Zero,
                lastCompletedAtUtc:
                    DateTimeOffset.UnixEpoch,
                lastOutcome:
                    (TransportExchangeOutcome)999);
        }

        // Assert
        ArgumentOutOfRangeException exception =
            Assert.Throws<
                ArgumentOutOfRangeException>(
                    Act);

        Assert.Equal(
            "lastOutcome",
            exception.ParamName);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Constructor_EmptyWithLastExchangeInformation_ShouldThrow(
        bool includeCompletionTimestamp,
        bool includeOutcome)
    {
        // Act
        void Act()
        {
            _ = new TransportExchangeStatistics(
                completedExchangeCount:
                    0,
                successfulExchangeCount:
                    0,
                failedExchangeCount:
                    0,
                cancelledExchangeCount:
                    0,
                totalRequestByteCount:
                    0,
                totalResponseByteCount:
                    0,
                totalDuration:
                    TimeSpan.Zero,
                lastCompletedAtUtc:
                    includeCompletionTimestamp
                        ? DateTimeOffset.UnixEpoch
                        : null,
                lastOutcome:
                    includeOutcome
                        ? TransportExchangeOutcome.Succeeded
                        : null);
        }

        // Assert
        ArgumentException exception =
            Assert.Throws<
                ArgumentException>(
                    Act);

        Assert.Equal(
            "lastCompletedAtUtc",
            exception.ParamName);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Constructor_NonEmptyWithoutCompleteLastExchangeInformation_ShouldThrow(
        bool includeCompletionTimestamp,
        bool includeOutcome)
    {
        // Act
        void Act()
        {
            _ = new TransportExchangeStatistics(
                completedExchangeCount:
                    1,
                successfulExchangeCount:
                    1,
                failedExchangeCount:
                    0,
                cancelledExchangeCount:
                    0,
                totalRequestByteCount:
                    10,
                totalResponseByteCount:
                    20,
                totalDuration:
                    TimeSpan.Zero,
                lastCompletedAtUtc:
                    includeCompletionTimestamp
                        ? DateTimeOffset.UnixEpoch
                        : null,
                lastOutcome:
                    includeOutcome
                        ? TransportExchangeOutcome.Succeeded
                        : null);
        }

        // Assert
        ArgumentException exception =
            Assert.Throws<
                ArgumentException>(
                    Act);

        Assert.Equal(
            "lastCompletedAtUtc",
            exception.ParamName);
    }
}
