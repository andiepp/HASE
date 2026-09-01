using System.IO;
using System.Net.Sockets;
using Hase.DesktopHost.App.Hosting;
using Hase.Runtime.Diagnostics;
using Hase.DesktopHost.Hosting;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostEndpointStartupCoordinatorTests
{
    [Fact]
    public async Task TryAttachAsync_Success_ShouldPublishNothingAndReturnTrue()
    {
        var context = new TestContext();
        int callCount = 0;

        bool attached = await context.Coordinator.TryAttachAsync(
            "endpoint-01",
            "NativeNetwork",
            _ =>
            {
                callCount++;
                return Task.CompletedTask;
            });

        Assert.True(attached);
        Assert.Equal(1, callCount);
        Assert.Empty(context.Collector.GetSnapshot());
    }

    [Theory]
    [MemberData(nameof(AvailabilityFailures))]
    public async Task TryAttachAsync_AvailabilityFailure_ShouldWarnAndContinue(
        Exception failure,
        string expectedCategory)
    {
        const string sensitiveFailureText = "sensitive transport target";
        var context = new TestContext();
        Exception actualFailure = WithSensitiveMessage(
            failure,
            sensitiveFailureText);

        bool attached = await context.Coordinator.TryAttachAsync(
            "endpoint-01",
            "NativeNetwork",
            _ => Task.FromException(actualFailure));

        Assert.False(attached);
        RuntimeDiagnosticRecord record = Assert.Single(
            context.Collector.GetSnapshot());
        Assert.Equal(
            RuntimeDiagnosticLevel.Operational,
            record.Level);
        Assert.Equal(
            RuntimeDiagnosticCategory.RuntimeAttachment,
            record.Category);
        Assert.Equal("EndpointStartupUnavailable", record.EventName);
        Assert.Equal(RuntimeDiagnosticSeverity.Warning, record.Severity);
        Assert.Equal(RuntimeDiagnosticOutcome.Failed, record.Outcome);
        Assert.Equal("endpoint-01", record.EndpointId);
        Assert.Equal("NativeNetwork", record.Details["EndpointKind"]);
        Assert.Equal(expectedCategory, record.Details["FailureCategory"]);
        Assert.DoesNotContain(
            sensitiveFailureText,
            string.Join("\n", record.Details.Values),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryAttachAsync_SeveralEndpoints_ShouldRetainOnlySuccessfulResults()
    {
        var context = new TestContext();
        var availableEndpoints = new List<string>();

        bool first = await context.Coordinator.TryAttachAsync(
            "first",
            "NativeNetwork",
            _ => Task.FromException(new TimeoutException()));
        bool second = await context.Coordinator.TryAttachAsync(
            "second",
            "CompactSerial",
            _ =>
            {
                availableEndpoints.Add("second");
                return Task.CompletedTask;
            });
        bool third = await context.Coordinator.TryAttachAsync(
            "third",
            "Kel103Serial",
            _ => Task.FromException(new IOException()));

        Assert.False(first);
        Assert.True(second);
        Assert.False(third);
        Assert.Equal(["second"], availableEndpoints);
        Assert.Equal(2, context.Collector.GetSnapshot().Count);
    }

    [Fact]
    public async Task TryAttachAsync_AllUnavailable_ShouldReturnFalseWithoutThrowing()
    {
        var context = new TestContext();

        bool first = await context.Coordinator.TryAttachAsync(
            "first",
            "NativeNetwork",
            _ => Task.FromException(new TimeoutException()));
        bool second = await context.Coordinator.TryAttachAsync(
            "second",
            "CompactSerial",
            _ => Task.FromException(
                new DesktopRuntimeHostEndpointUnavailableException(
                    "NoVerifiedCandidate")));

        Assert.False(first);
        Assert.False(second);
        Assert.Equal(2, context.Collector.GetSnapshot().Count);
    }

    [Theory]
    [MemberData(nameof(FatalFailures))]
    public async Task TryAttachAsync_FatalFailure_ShouldPropagateWithoutWarning(
        Exception failure)
    {
        var context = new TestContext();

        Exception actual = await Assert.ThrowsAsync(
            failure.GetType(),
            () => context.Coordinator.TryAttachAsync(
                "endpoint-01",
                "NativeNetwork",
                _ => Task.FromException(failure)));

        Assert.Same(failure, actual);
        Assert.Empty(context.Collector.GetSnapshot());
    }

    [Fact]
    public async Task TryAttachAsync_MixedAggregate_ShouldPropagateWithoutWarning()
    {
        var context = new TestContext();
        var failure = new AggregateException(
            new TimeoutException(),
            new InvalidDataException());

        AggregateException actual =
            await Assert.ThrowsAsync<AggregateException>(
                () => context.Coordinator.TryAttachAsync(
                    "endpoint-01",
                    "NativeNetwork",
                    _ => Task.FromException(failure)));

        Assert.Same(failure, actual);
        Assert.Empty(context.Collector.GetSnapshot());
    }

    [Fact]
    public async Task TryAttachAsync_CallerCancellation_ShouldPropagateWithoutWarning()
    {
        var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        int callCount = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Coordinator.TryAttachAsync(
                "endpoint-01",
                "NativeNetwork",
                _ =>
                {
                    callCount++;
                    return Task.CompletedTask;
                },
                cancellation.Token));

        Assert.Equal(0, callCount);
        Assert.Empty(context.Collector.GetSnapshot());
    }

    [Fact]
    public async Task TryAttachAsync_InternalCancellation_ShouldBeUnavailableTimeout()
    {
        var context = new TestContext();

        bool attached = await context.Coordinator.TryAttachAsync(
            "endpoint-01",
            "NativeNetwork",
            _ => Task.FromException(new OperationCanceledException()));

        Assert.False(attached);
        RuntimeDiagnosticRecord record = Assert.Single(
            context.Collector.GetSnapshot());
        Assert.Equal("TimedOut", record.Details["FailureCategory"]);
    }

    [Fact]
    public async Task TryAttachAsync_AllUnavailableAggregate_ShouldWarnAndContinue()
    {
        var context = new TestContext();
        var failure = new AggregateException(
            new TimeoutException(),
            new IOException());

        bool attached = await context.Coordinator.TryAttachAsync(
            "endpoint-01",
            "NativeNetwork",
            _ => Task.FromException(failure));

        Assert.False(attached);
        RuntimeDiagnosticRecord record = Assert.Single(
            context.Collector.GetSnapshot());
        Assert.Equal(
            "MultipleAvailabilityFailures",
            record.Details["FailureCategory"]);
    }

    public static IEnumerable<object[]> AvailabilityFailures()
    {
        yield return [new TimeoutException(), "TimedOut"];
        yield return [new OperationCanceledException(), "TimedOut"];
        yield return [new SocketException(), "NetworkUnavailable"];
        yield return [new UnauthorizedAccessException(), "AccessUnavailable"];
        yield return [new IOException(), "IoUnavailable"];
        yield return
        [
            new DesktopRuntimeHostEndpointUnavailableException(
                "NoVerifiedCandidate"),
            "NoVerifiedCandidate"
        ];
    }

    public static IEnumerable<object[]> FatalFailures()
    {
        yield return [new InvalidDataException("identity mismatch")];
        yield return [new InvalidOperationException("ambiguous endpoint")];
        yield return [new NotSupportedException("unsupported endpoint")];
        yield return [new ArgumentException("invalid configuration")];
    }

    private static Exception WithSensitiveMessage(
        Exception failure,
        string sensitiveMessage) =>
        failure switch
        {
            TimeoutException => new TimeoutException(sensitiveMessage),
            OperationCanceledException =>
                new OperationCanceledException(sensitiveMessage),
            SocketException =>
                new SocketException((int)SocketError.HostUnreachable),
            UnauthorizedAccessException =>
                new UnauthorizedAccessException(sensitiveMessage),
            IOException => new IOException(sensitiveMessage),
            DesktopRuntimeHostEndpointUnavailableException unavailable =>
                new DesktopRuntimeHostEndpointUnavailableException(
                    unavailable.FailureCategory),
            _ => failure
        };

    private sealed class TestContext
    {
        public TestContext()
        {
            Collector = new BoundedRuntimeDiagnosticCollector(20);
            Coordinator = new DesktopRuntimeHostEndpointStartupCoordinator(
                new RuntimeDiagnosticPublisher(Collector));
        }

        public BoundedRuntimeDiagnosticCollector Collector { get; }

        public DesktopRuntimeHostEndpointStartupCoordinator Coordinator
        {
            get;
        }
    }
}
