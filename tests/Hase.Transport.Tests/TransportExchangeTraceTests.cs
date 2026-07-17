namespace Hase.Transport.Tests;

public sealed class TransportExchangeTraceTests
{
    private static readonly DateTimeOffset StartedAtUtc =
        DateTimeOffset.FromUnixTimeMilliseconds(
            1_750_000_000_000);

    private static readonly DateTimeOffset CompletedAtUtc =
        StartedAtUtc.AddMilliseconds(
            250);

    [Fact]
    public void Constructor_SuccessfulExchange_ShouldPreserveValues()
    {
        var trace =
            new TransportExchangeTrace(
                sequenceNumber:
                    3,
                startedAtUtc:
                    StartedAtUtc,
                completedAtUtc:
                    CompletedAtUtc,
                duration:
                    TimeSpan.FromMilliseconds(
                        200),
                requestByteCount:
                    32,
                responseByteCount:
                    128,
                outcome:
                    TransportExchangeOutcome.Succeeded,
                connectionState:
                    TransportConnectionState.Connected);

        Assert.Equal(
            3,
            trace.SequenceNumber);

        Assert.Equal(
            StartedAtUtc,
            trace.StartedAtUtc);

        Assert.Equal(
            CompletedAtUtc,
            trace.CompletedAtUtc);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                200),
            trace.Duration);

        Assert.Equal(
            32,
            trace.RequestByteCount);

        Assert.Equal(
            128,
            trace.ResponseByteCount);

        Assert.Equal(
            TransportExchangeOutcome.Succeeded,
            trace.Outcome);

        Assert.Equal(
            TransportConnectionState.Connected,
            trace.ConnectionState);

        Assert.Null(
            trace.ExceptionType);

        Assert.Null(
            trace.ExceptionMessage);
    }

    [Fact]
    public void Constructor_FailedExchange_ShouldNormalizeExceptionInformation()
    {
        var trace =
            new TransportExchangeTrace(
                sequenceNumber:
                    1,
                startedAtUtc:
                    StartedAtUtc,
                completedAtUtc:
                    CompletedAtUtc,
                duration:
                    TimeSpan.FromMilliseconds(
                        250),
                requestByteCount:
                    16,
                responseByteCount:
                    0,
                outcome:
                    TransportExchangeOutcome.Failed,
                connectionState:
                    TransportConnectionState.Faulted,
                exceptionType:
                    "  System.IO.IOException  ",
                exceptionMessage:
                    "  Connection lost.  ");

        Assert.Equal(
            "System.IO.IOException",
            trace.ExceptionType);

        Assert.Equal(
            "Connection lost.",
            trace.ExceptionMessage);
    }

    [Fact]
    public void Constructor_CancelledExchange_ShouldAllowMissingMessage()
    {
        var trace =
            new TransportExchangeTrace(
                sequenceNumber:
                    1,
                startedAtUtc:
                    StartedAtUtc,
                completedAtUtc:
                    CompletedAtUtc,
                duration:
                    TimeSpan.FromMilliseconds(
                        100),
                requestByteCount:
                    16,
                responseByteCount:
                    0,
                outcome:
                    TransportExchangeOutcome.Cancelled,
                connectionState:
                    TransportConnectionState.Faulted,
                exceptionType:
                    typeof(OperationCanceledException).FullName);

        Assert.Equal(
            TransportExchangeOutcome.Cancelled,
            trace.Outcome);

        Assert.Equal(
            typeof(OperationCanceledException).FullName,
            trace.ExceptionType);

        Assert.Null(
            trace.ExceptionMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidSequenceNumber_ShouldThrow(
        long sequenceNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateSuccessfulTrace(
                sequenceNumber:
                    sequenceNumber));
    }

    [Fact]
    public void Constructor_NonUtcStartTimestamp_ShouldThrow()
    {
        DateTimeOffset timestamp =
            StartedAtUtc.ToOffset(
                TimeSpan.FromHours(
                    2));

        Assert.Throws<ArgumentException>(
            () =>
                new TransportExchangeTrace(
                    1,
                    timestamp,
                    CompletedAtUtc,
                    TimeSpan.Zero,
                    0,
                    0,
                    TransportExchangeOutcome.Succeeded,
                    TransportConnectionState.Connected));
    }

    [Fact]
    public void Constructor_NonUtcCompletionTimestamp_ShouldThrow()
    {
        DateTimeOffset timestamp =
            CompletedAtUtc.ToOffset(
                TimeSpan.FromHours(
                    2));

        Assert.Throws<ArgumentException>(
            () =>
                new TransportExchangeTrace(
                    1,
                    StartedAtUtc,
                    timestamp,
                    TimeSpan.Zero,
                    0,
                    0,
                    TransportExchangeOutcome.Succeeded,
                    TransportConnectionState.Connected));
    }

    [Fact]
    public void Constructor_CompletionBeforeStart_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new TransportExchangeTrace(
                    1,
                    StartedAtUtc,
                    StartedAtUtc.AddTicks(
                        -1),
                    TimeSpan.Zero,
                    0,
                    0,
                    TransportExchangeOutcome.Succeeded,
                    TransportConnectionState.Connected));
    }

    [Fact]
    public void Constructor_NegativeDuration_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new TransportExchangeTrace(
                    1,
                    StartedAtUtc,
                    CompletedAtUtc,
                    TimeSpan.FromTicks(
                        -1),
                    0,
                    0,
                    TransportExchangeOutcome.Succeeded,
                    TransportConnectionState.Connected));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Constructor_NegativeByteCount_ShouldThrow(
        int requestByteCount,
        int responseByteCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new TransportExchangeTrace(
                    1,
                    StartedAtUtc,
                    CompletedAtUtc,
                    TimeSpan.Zero,
                    requestByteCount,
                    responseByteCount,
                    TransportExchangeOutcome.Succeeded,
                    TransportConnectionState.Connected));
    }

    [Fact]
    public void Constructor_UndefinedOutcome_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new TransportExchangeTrace(
                    1,
                    StartedAtUtc,
                    CompletedAtUtc,
                    TimeSpan.Zero,
                    0,
                    0,
                    (TransportExchangeOutcome)999,
                    TransportConnectionState.Connected));
    }

    [Fact]
    public void Constructor_UndefinedConnectionState_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new TransportExchangeTrace(
                    1,
                    StartedAtUtc,
                    CompletedAtUtc,
                    TimeSpan.Zero,
                    0,
                    0,
                    TransportExchangeOutcome.Succeeded,
                    (TransportConnectionState)999));
    }

    [Theory]
    [InlineData("System.Exception", null)]
    [InlineData(null, "Unexpected exception.")]
    [InlineData("System.Exception", "Unexpected exception.")]
    public void Constructor_SuccessWithExceptionInformation_ShouldThrow(
        string? exceptionType,
        string? exceptionMessage)
    {
        Assert.Throws<ArgumentException>(
            () =>
                new TransportExchangeTrace(
                    1,
                    StartedAtUtc,
                    CompletedAtUtc,
                    TimeSpan.Zero,
                    0,
                    0,
                    TransportExchangeOutcome.Succeeded,
                    TransportConnectionState.Connected,
                    exceptionType,
                    exceptionMessage));
    }

    [Theory]
    [InlineData(TransportExchangeOutcome.Failed)]
    [InlineData(TransportExchangeOutcome.Cancelled)]
    public void Constructor_NonSuccessWithoutExceptionType_ShouldThrow(
        TransportExchangeOutcome outcome)
    {
        Assert.Throws<ArgumentException>(
            () =>
                new TransportExchangeTrace(
                    1,
                    StartedAtUtc,
                    CompletedAtUtc,
                    TimeSpan.Zero,
                    0,
                    0,
                    outcome,
                    TransportConnectionState.Faulted));
    }

    private static TransportExchangeTrace CreateSuccessfulTrace(
        long sequenceNumber)
    {
        return new TransportExchangeTrace(
            sequenceNumber,
            StartedAtUtc,
            CompletedAtUtc,
            TimeSpan.Zero,
            0,
            0,
            TransportExchangeOutcome.Succeeded,
            TransportConnectionState.Connected);
    }
}