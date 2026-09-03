using System.IO;
using Hase.DesktopHost.App.Hosting;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostEndpointRefreshCoordinatorTests
{
    [Fact]
    public async Task RefreshAsync_AbsentEndpoint_ShouldAttachAndPublishSuccess()
    {
        var context = new TestContext();
        int attachCount = 0;
        DesktopRuntimeHostEndpointRefreshTarget target =
            context.CreateTarget(
                "endpoint-01",
                "CompactSerial",
                _ =>
                {
                    attachCount++;
                    context.Published.Add("endpoint-01");
                    return Task.CompletedTask;
                });

        await context.Coordinator.RefreshAsync([target]);

        Assert.Equal(1, attachCount);
        Assert.Equal(
            [
                "EndpointRefreshStarted",
                "EndpointRefreshAttached",
                "EndpointRefreshCompleted"
            ],
            context.Events());
        RuntimeDiagnosticRecord completed = context.Records().Last();
        Assert.Equal("1", completed.Details["AttachedCount"]);
        Assert.Equal("0", completed.Details["SkippedCount"]);
        Assert.Equal(RuntimeDiagnosticOutcome.Succeeded, completed.Outcome);
    }

    [Fact]
    public async Task RefreshAsync_PublishedEndpoint_ShouldSkipWithoutAttaching()
    {
        var context = new TestContext();
        context.Published.Add("endpoint-01");
        int attachCount = 0;
        DesktopRuntimeHostEndpointRefreshTarget target =
            context.CreateTarget(
                "endpoint-01",
                "NativeNetwork",
                _ =>
                {
                    attachCount++;
                    return Task.CompletedTask;
                });

        await context.Coordinator.RefreshAsync([target]);

        Assert.Equal(0, attachCount);
        Assert.Contains(
            "EndpointRefreshSkippedPublished",
            context.Events());
        RuntimeDiagnosticRecord skipped = context.Records()[1];
        Assert.Equal("endpoint-01", skipped.EndpointId);
        Assert.Equal("NativeNetwork", skipped.Details["EndpointKind"]);
        Assert.Equal("1", context.Records().Last().Details["SkippedCount"]);
    }

    [Fact]
    public async Task RefreshAsync_UnavailableEndpoint_ShouldNotBlockLaterEndpoint()
    {
        var context = new TestContext();
        DesktopRuntimeHostEndpointRefreshTarget unavailable =
            context.CreateTarget(
                "unavailable",
                "NativeNetwork",
                _ => Task.FromException(new TimeoutException("sensitive")));
        DesktopRuntimeHostEndpointRefreshTarget available =
            context.CreateTarget(
                "available",
                "OtherSerial",
                _ =>
                {
                    context.Published.Add("available");
                    return Task.CompletedTask;
                });

        await context.Coordinator.RefreshAsync(
            [unavailable, available]);

        Assert.Contains("EndpointRefreshUnavailable", context.Events());
        Assert.Contains("EndpointRefreshAttached", context.Events());
        RuntimeDiagnosticRecord unavailableRecord =
            context.Records()
                .Single(record =>
                    record.EventName == "EndpointRefreshUnavailable");
        Assert.Equal("TimedOut", unavailableRecord.Details["FailureCategory"]);
        Assert.DoesNotContain(
            "sensitive",
            string.Join("\n", unavailableRecord.Details.Values),
            StringComparison.Ordinal);
        RuntimeDiagnosticRecord completed = context.Records().Last();
        Assert.Equal("1", completed.Details["AttachedCount"]);
        Assert.Equal("1", completed.Details["UnavailableCount"]);
        Assert.Equal(RuntimeDiagnosticOutcome.Succeeded, completed.Outcome);
    }

    [Fact]
    public async Task RefreshAsync_WrongPublishedIdentity_ShouldFailClosedAndContinue()
    {
        var context = new TestContext();
        DesktopRuntimeHostEndpointRefreshTarget wrongIdentity =
            context.CreateTarget(
                "expected",
                "CompactSerial",
                _ =>
                {
                    context.Published.Add("different");
                    return Task.CompletedTask;
                });
        DesktopRuntimeHostEndpointRefreshTarget available =
            context.CreateTarget(
                "available",
                "NativeNetwork",
                _ =>
                {
                    context.Published.Add("available");
                    return Task.CompletedTask;
                });

        await context.Coordinator.RefreshAsync(
            [wrongIdentity, available]);

        RuntimeDiagnosticRecord failed =
            context.Records()
                .Single(record =>
                    record.EventName == "EndpointRefreshFailed");
        Assert.Equal(
            "AuthoritativeIdentityRejected",
            failed.Details["FailureCategory"]);
        Assert.Contains("available", context.Published);
        RuntimeDiagnosticRecord completed = context.Records().Last();
        Assert.Equal("1", completed.Details["FailedCount"]);
        Assert.Equal("1", completed.Details["AttachedCount"]);
        Assert.Equal(RuntimeDiagnosticOutcome.Failed, completed.Outcome);
    }

    [Fact]
    public async Task RefreshAsync_CallerCancellation_ShouldPublishAndPropagate()
    {
        var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        var entered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        DesktopRuntimeHostEndpointRefreshTarget target =
            context.CreateTarget(
                "endpoint-01",
                "NativeNetwork",
                async token =>
                {
                    entered.SetResult(true);
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                });

        Task refresh = context.Coordinator.RefreshAsync(
            [target],
            cancellation.Token);
        await entered.Task;
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => refresh);
        RuntimeDiagnosticRecord cancelled = context.Records().Last();
        Assert.Equal("EndpointRefreshCancelled", cancelled.EventName);
        Assert.Equal(RuntimeDiagnosticOutcome.Cancelled, cancelled.Outcome);
    }

    [Fact]
    public async Task RefreshAsync_ConcurrentRequests_ShouldRunSerially()
    {
        var context = new TestContext();
        var entered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var release =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        int attachCount = 0;
        DesktopRuntimeHostEndpointRefreshTarget target =
            context.CreateTarget(
                "endpoint-01",
                "CompactSerial",
                async _ =>
                {
                    attachCount++;
                    entered.SetResult(true);
                    await release.Task;
                    context.Published.Add("endpoint-01");
                });

        Task first = context.Coordinator.RefreshAsync([target]);
        await entered.Task;
        Task second = context.Coordinator.RefreshAsync([target]);

        Assert.Equal(1, attachCount);
        release.SetResult(true);
        await Task.WhenAll(first, second);

        Assert.Equal(1, attachCount);
        Assert.Equal(
            2,
            context.Events().Count(eventName =>
                eventName == "EndpointRefreshStarted"));
        Assert.Contains(
            "EndpointRefreshSkippedPublished",
            context.Events());
    }

    [Fact]
    public async Task RefreshAsync_EmptyTargets_ShouldPublishZeroSummary()
    {
        var context = new TestContext();

        await context.Coordinator.RefreshAsync([]);

        Assert.Equal(
            ["EndpointRefreshStarted", "EndpointRefreshCompleted"],
            context.Events());
        RuntimeDiagnosticRecord completed = context.Records().Last();
        Assert.Equal("0", completed.Details["TargetCount"]);
        Assert.Equal("0", completed.Details["AttachedCount"]);
    }

    private sealed class TestContext
    {
        public TestContext()
        {
            Collector = new BoundedRuntimeDiagnosticCollector(100);
            Coordinator = new DesktopRuntimeHostEndpointRefreshCoordinator(
                endpointId => Published.Contains(endpointId),
                new RuntimeDiagnosticPublisher(Collector));
        }

        public HashSet<string> Published { get; } =
            new(StringComparer.Ordinal);

        public BoundedRuntimeDiagnosticCollector Collector { get; }

        public DesktopRuntimeHostEndpointRefreshCoordinator Coordinator
        {
            get;
        }

        public DesktopRuntimeHostEndpointRefreshTarget CreateTarget(
            string endpointId,
            string endpointKind,
            Func<CancellationToken, Task> attachAsync) =>
            new(endpointId, endpointKind, attachAsync);

        public IReadOnlyList<RuntimeDiagnosticRecord> Records() =>
            Collector.GetSnapshot();

        public IReadOnlyList<string> Events() =>
            Records()
                .Select(record => record.EventName)
                .ToArray();
    }
}
