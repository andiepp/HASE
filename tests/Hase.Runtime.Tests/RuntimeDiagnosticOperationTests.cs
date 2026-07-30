using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Tests;

public sealed class RuntimeDiagnosticOperationTests
{
    [Fact]
    public void Construction_PublishesCorrelatedStartedRecord()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                4);

        RuntimeDiagnosticOperation operation =
            CreateOperation(
                collector,
                details:
                    new Dictionary<string, string>
                    {
                        ["instrument"] = "scope.channel",
                        ["path"] = "value"
                    });

        RuntimeDiagnosticRecord record =
            Assert.Single(
                collector.GetSnapshot());

        Assert.Equal(
            "PropertyReadStarted",
            record.EventName);
        Assert.Equal(
            operation.OperationId,
            record.OperationId);
        Assert.Equal(
            "endpoint-1",
            record.EndpointId);
        Assert.Null(
            record.Duration);
        Assert.Null(
            record.Outcome);
        Assert.Equal(
            "scope.channel",
            record.Details["instrument"]);
        Assert.Equal(
            "value",
            record.Details["path"]);
    }

    [Fact]
    public void Complete_SucceededPublishesCompletedRecordWithDuration()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                4);

        ManualTimeProvider timeProvider =
            new();

        RuntimeDiagnosticOperation operation =
            CreateOperation(
                collector,
                timeProvider);

        timeProvider.Advance(
            TimeSpan.FromMilliseconds(
                125));

        operation.Complete(
            RuntimeDiagnosticOutcome.Succeeded);

        RuntimeDiagnosticRecord completed =
            collector
                .GetSnapshot()
                .Last();

        Assert.Equal(
            "PropertyReadCompleted",
            completed.EventName);
        Assert.Equal(
            RuntimeDiagnosticSeverity.Information,
            completed.Severity);
        Assert.Equal(
            RuntimeDiagnosticOutcome.Succeeded,
            completed.Outcome);
        Assert.Equal(
            TimeSpan.FromMilliseconds(
                125),
            completed.Duration);
        Assert.Equal(
            operation.OperationId,
            completed.OperationId);
    }

    [Fact]
    public void Complete_FailurePublishesFailedRecordOnlyOnce()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                4);

        RuntimeDiagnosticOperation operation =
            CreateOperation(
                collector);

        operation.Complete(
            RuntimeDiagnosticOutcome.Failed);

        operation.Complete(
            RuntimeDiagnosticOutcome.Succeeded);

        IReadOnlyList<RuntimeDiagnosticRecord> records =
            collector.GetSnapshot();

        Assert.Equal(
            2,
            records.Count);
        Assert.Equal(
            "PropertyReadFailed",
            records[1].EventName);
        Assert.Equal(
            RuntimeDiagnosticSeverity.Warning,
            records[1].Severity);
        Assert.Equal(
            RuntimeDiagnosticOutcome.Failed,
            records[1].Outcome);
    }

    [Fact]
    public async Task RunAsync_SuccessReturnsResult()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                4);

        RuntimeDiagnosticOperation operation =
            CreateOperation(
                collector);

        int result =
            await operation.RunAsync(
                _ =>
                    Task.FromResult(
                        42));

        Assert.Equal(
            42,
            result);
        Assert.Equal(
            RuntimeDiagnosticOutcome.Succeeded,
            collector.GetSnapshot()[1].Outcome);
    }

    [Fact]
    public async Task RunAsync_FailureRethrowsOriginalExceptionWithoutDetails()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                4);

        RuntimeDiagnosticOperation operation =
            CreateOperation(
                collector);

        InvalidOperationException expected =
            new(
                "sensitive text");

        InvalidOperationException actual =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    operation.RunAsync(
                        _ =>
                            Task.FromException(
                                expected)));

        Assert.Same(
            expected,
            actual);

        RuntimeDiagnosticRecord failed =
            collector.GetSnapshot()[1];

        Assert.Equal(
            RuntimeDiagnosticOutcome.Failed,
            failed.Outcome);
        Assert.DoesNotContain(
            failed.Details,
            detail =>
                detail.Value.Contains(
                    "sensitive text",
                    StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsync_ClassifiesCancellationAndTimeout(
        bool cancelled)
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                4);

        RuntimeDiagnosticOperation operation =
            CreateOperation(
                collector);

        Exception expected =
            cancelled
                ? new OperationCanceledException()
                : new TimeoutException();

        Exception actual =
            await Assert.ThrowsAnyAsync<Exception>(
                () =>
                    operation.RunAsync(
                        _ =>
                            Task.FromException(
                                expected)));

        Assert.Same(
            expected,
            actual);
        Assert.Equal(
            cancelled
                ? RuntimeDiagnosticOutcome.Cancelled
                : RuntimeDiagnosticOutcome.TimedOut,
            collector.GetSnapshot()[1].Outcome);
    }

    [Fact]
    public async Task RunAsync_ObserverFailureDoesNotChangeRuntimeResult()
    {
        RuntimeDiagnosticOperation operation =
            new(
                new RuntimeDiagnosticPublisher(
                    new ThrowingSink()),
                RuntimeDiagnosticCategory.RuntimeProperty,
                "PropertyReadStarted",
                "PropertyReadCompleted",
                "PropertyReadFailed");

        int result =
            await operation.RunAsync(
                _ =>
                    Task.FromResult(
                        42));

        Assert.Equal(
            42,
            result);
    }

    private static RuntimeDiagnosticOperation CreateOperation(
        BoundedRuntimeDiagnosticCollector collector,
        ManualTimeProvider? timeProvider = null,
        IReadOnlyDictionary<string, string>? details = null)
    {
        return new RuntimeDiagnosticOperation(
            new RuntimeDiagnosticPublisher(
                collector),
            RuntimeDiagnosticCategory.RuntimeProperty,
            "PropertyReadStarted",
            "PropertyReadCompleted",
            "PropertyReadFailed",
            "endpoint-1",
            null,
            null,
            details,
            timeProvider ??
            new ManualTimeProvider());
    }

    private sealed class ManualTimeProvider :
        TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency =>
            TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return timestamp;
        }

        public void Advance(
            TimeSpan duration)
        {
            timestamp +=
                duration.Ticks;
        }
    }

    private sealed class ThrowingSink :
        IRuntimeDiagnosticSink
    {
        public bool IsEnabled(
            RuntimeDiagnosticLevel level)
        {
            return true;
        }

        public void Publish(
            RuntimeDiagnosticRecord record)
        {
            throw new InvalidOperationException(
                "Observer failure.");
        }
    }
}
