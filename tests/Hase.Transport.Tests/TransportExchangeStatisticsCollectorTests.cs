namespace Hase.Transport.Tests;

public sealed class TransportExchangeStatisticsCollectorTests
{
    [Fact]
    public void NewCollector_ShouldReturnEmptyStatistics()
    {
        // Arrange
        var collector =
            new TransportExchangeStatisticsCollector();

        // Act
        TransportExchangeStatistics statistics =
            collector.GetStatistics();

        // Assert
        Assert.Equal(
            TransportExchangeStatistics.Empty,
            statistics);
    }

    [Fact]
    public void OnTransportExchangeCompleted_NullTrace_ShouldThrow()
    {
        // Arrange
        var collector =
            new TransportExchangeStatisticsCollector();

        // Act
        void Act()
        {
            collector.OnTransportExchangeCompleted(
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<
                ArgumentNullException>(
                    Act);

        Assert.Equal(
            "trace",
            exception.ParamName);

        Assert.Equal(
            TransportExchangeStatistics.Empty,
            collector.GetStatistics());
    }

    [Fact]
    public void OnTransportExchangeCompleted_SuccessfulTrace_ShouldCollectStatistics()
    {
        // Arrange
        var collector =
            new TransportExchangeStatisticsCollector();

        DateTimeOffset completedAtUtc =
            DateTimeOffset.UnixEpoch.AddSeconds(
                1);

        TransportExchangeTrace trace =
            CreateTrace(
                sequenceNumber:
                    1,
                completedAtUtc:
                    completedAtUtc,
                duration:
                    TimeSpan.FromMilliseconds(
                        25),
                requestByteCount:
                    32,
                responseByteCount:
                    967,
                outcome:
                    TransportExchangeOutcome.Succeeded);

        // Act
        collector.OnTransportExchangeCompleted(
            trace);

        TransportExchangeStatistics statistics =
            collector.GetStatistics();

        // Assert
        Assert.Equal(
            1,
            statistics.CompletedExchangeCount);

        Assert.Equal(
            1,
            statistics.SuccessfulExchangeCount);

        Assert.Equal(
            0,
            statistics.FailedExchangeCount);

        Assert.Equal(
            0,
            statistics.CancelledExchangeCount);

        Assert.Equal(
            32,
            statistics.TotalRequestByteCount);

        Assert.Equal(
            967,
            statistics.TotalResponseByteCount);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                25),
            statistics.TotalDuration);

        Assert.Equal(
            completedAtUtc,
            statistics.LastCompletedAtUtc);

        Assert.Equal(
            TransportExchangeOutcome.Succeeded,
            statistics.LastOutcome);
    }

    [Fact]
    public void OnTransportExchangeCompleted_MultipleOutcomes_ShouldAggregateStatistics()
    {
        // Arrange
        var collector =
            new TransportExchangeStatisticsCollector();

        DateTimeOffset firstCompletedAtUtc =
            DateTimeOffset.UnixEpoch.AddSeconds(
                1);

        DateTimeOffset secondCompletedAtUtc =
            DateTimeOffset.UnixEpoch.AddSeconds(
                2);

        DateTimeOffset thirdCompletedAtUtc =
            DateTimeOffset.UnixEpoch.AddSeconds(
                3);

        TransportExchangeTrace successfulTrace =
            CreateTrace(
                sequenceNumber:
                    1,
                completedAtUtc:
                    firstCompletedAtUtc,
                duration:
                    TimeSpan.FromMilliseconds(
                        10),
                requestByteCount:
                    20,
                responseByteCount:
                    100,
                outcome:
                    TransportExchangeOutcome.Succeeded);

        TransportExchangeTrace failedTrace =
            CreateTrace(
                sequenceNumber:
                    2,
                completedAtUtc:
                    secondCompletedAtUtc,
                duration:
                    TimeSpan.FromMilliseconds(
                        20),
                requestByteCount:
                    30,
                responseByteCount:
                    0,
                outcome:
                    TransportExchangeOutcome.Failed);

        TransportExchangeTrace cancelledTrace =
            CreateTrace(
                sequenceNumber:
                    3,
                completedAtUtc:
                    thirdCompletedAtUtc,
                duration:
                    TimeSpan.FromMilliseconds(
                        30),
                requestByteCount:
                    40,
                responseByteCount:
                    0,
                outcome:
                    TransportExchangeOutcome.Cancelled);

        // Act
        collector.OnTransportExchangeCompleted(
            successfulTrace);

        collector.OnTransportExchangeCompleted(
            failedTrace);

        collector.OnTransportExchangeCompleted(
            cancelledTrace);

        TransportExchangeStatistics statistics =
            collector.GetStatistics();

        // Assert
        Assert.Equal(
            3,
            statistics.CompletedExchangeCount);

        Assert.Equal(
            1,
            statistics.SuccessfulExchangeCount);

        Assert.Equal(
            1,
            statistics.FailedExchangeCount);

        Assert.Equal(
            1,
            statistics.CancelledExchangeCount);

        Assert.Equal(
            90,
            statistics.TotalRequestByteCount);

        Assert.Equal(
            100,
            statistics.TotalResponseByteCount);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                60),
            statistics.TotalDuration);

        Assert.Equal(
            thirdCompletedAtUtc,
            statistics.LastCompletedAtUtc);

        Assert.Equal(
            TransportExchangeOutcome.Cancelled,
            statistics.LastOutcome);
    }

    [Fact]
    public void GetStatistics_PreviousSnapshot_ShouldRemainImmutable()
    {
        // Arrange
        var collector =
            new TransportExchangeStatisticsCollector();

        collector.OnTransportExchangeCompleted(
            CreateTrace(
                sequenceNumber:
                    1,
                completedAtUtc:
                    DateTimeOffset.UnixEpoch.AddSeconds(
                        1),
                duration:
                    TimeSpan.FromMilliseconds(
                        10),
                requestByteCount:
                    20,
                responseByteCount:
                    100,
                outcome:
                    TransportExchangeOutcome.Succeeded));

        TransportExchangeStatistics firstSnapshot =
            collector.GetStatistics();

        // Act
        collector.OnTransportExchangeCompleted(
            CreateTrace(
                sequenceNumber:
                    2,
                completedAtUtc:
                    DateTimeOffset.UnixEpoch.AddSeconds(
                        2),
                duration:
                    TimeSpan.FromMilliseconds(
                        20),
                requestByteCount:
                    30,
                responseByteCount:
                    0,
                outcome:
                    TransportExchangeOutcome.Failed));

        TransportExchangeStatistics secondSnapshot =
            collector.GetStatistics();

        // Assert
        Assert.Equal(
            1,
            firstSnapshot.CompletedExchangeCount);

        Assert.Equal(
            1,
            firstSnapshot.SuccessfulExchangeCount);

        Assert.Equal(
            0,
            firstSnapshot.FailedExchangeCount);

        Assert.Equal(
            20,
            firstSnapshot.TotalRequestByteCount);

        Assert.Equal(
            100,
            firstSnapshot.TotalResponseByteCount);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                10),
            firstSnapshot.TotalDuration);

        Assert.Equal(
            2,
            secondSnapshot.CompletedExchangeCount);

        Assert.Equal(
            1,
            secondSnapshot.SuccessfulExchangeCount);

        Assert.Equal(
            1,
            secondSnapshot.FailedExchangeCount);

        Assert.Equal(
            50,
            secondSnapshot.TotalRequestByteCount);

        Assert.Equal(
            100,
            secondSnapshot.TotalResponseByteCount);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                30),
            secondSnapshot.TotalDuration);
    }

    [Fact]
    public async Task ConcurrentPublication_ShouldCollectEveryTrace()
    {
        // Arrange
        const int traceCount =
            1000;

        var collector =
            new TransportExchangeStatisticsCollector();

        // Act
        Task[] tasks =
            Enumerable.Range(
                    1,
                    traceCount)
                .Select(
                    sequenceNumber =>
                        Task.Run(
                            () =>
                                collector.OnTransportExchangeCompleted(
                                    CreateTrace(
                                        sequenceNumber:
                                            sequenceNumber,
                                        completedAtUtc:
                                            DateTimeOffset.UnixEpoch.AddTicks(
                                                sequenceNumber),
                                        duration:
                                            TimeSpan.FromTicks(
                                                1),
                                        requestByteCount:
                                            2,
                                        responseByteCount:
                                            3,
                                        outcome:
                                            TransportExchangeOutcome.Succeeded))))
                .ToArray();

        await Task.WhenAll(
            tasks);

        TransportExchangeStatistics statistics =
            collector.GetStatistics();

        // Assert
        Assert.Equal(
            traceCount,
            statistics.CompletedExchangeCount);

        Assert.Equal(
            traceCount,
            statistics.SuccessfulExchangeCount);

        Assert.Equal(
            0,
            statistics.FailedExchangeCount);

        Assert.Equal(
            0,
            statistics.CancelledExchangeCount);

        Assert.Equal(
            traceCount * 2,
            statistics.TotalRequestByteCount);

        Assert.Equal(
            traceCount * 3,
            statistics.TotalResponseByteCount);

        Assert.Equal(
            TimeSpan.FromTicks(
                traceCount),
            statistics.TotalDuration);

        Assert.NotNull(
            statistics.LastCompletedAtUtc);

        Assert.Equal(
            TransportExchangeOutcome.Succeeded,
            statistics.LastOutcome);
    }

    private static TransportExchangeTrace CreateTrace(
        long sequenceNumber,
        DateTimeOffset completedAtUtc,
        TimeSpan duration,
        int requestByteCount,
        int responseByteCount,
        TransportExchangeOutcome outcome)
    {
        string? exceptionType =
            outcome == TransportExchangeOutcome.Succeeded
                ? null
                : typeof(IOException).FullName;

        string? exceptionMessage =
            outcome == TransportExchangeOutcome.Succeeded
                ? null
                : "The transport exchange did not complete successfully.";

        return new TransportExchangeTrace(
            sequenceNumber:
                sequenceNumber,
            startedAtUtc:
                completedAtUtc.Subtract(
                    duration),
            completedAtUtc:
                completedAtUtc,
            duration:
                duration,
            requestByteCount:
                requestByteCount,
            responseByteCount:
                responseByteCount,
            outcome:
                outcome,
            connectionState:
                outcome == TransportExchangeOutcome.Succeeded
                    ? TransportConnectionState.Connected
                    : TransportConnectionState.Faulted,
            exceptionType:
                exceptionType,
            exceptionMessage:
                exceptionMessage);
    }
}