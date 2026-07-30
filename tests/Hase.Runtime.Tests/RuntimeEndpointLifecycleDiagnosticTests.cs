using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Tests;

public sealed class RuntimeEndpointLifecycleDiagnosticTests
{
    private static readonly DateTimeOffset Timestamp =
        new(
            2026,
            7,
            30,
            15,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void AddAndRemoveEndpoint_PublishesInventoryDiagnostics()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                10);

        RuntimeContext context =
            new(
                new RuntimeDiagnosticPublisher(
                    collector));

        RuntimeEndpoint endpoint =
            context.AddEndpoint(
                CreateDescriptor());

        Assert.True(
            context.RemoveEndpoint(
                endpoint));

        IReadOnlyList<RuntimeDiagnosticRecord> records =
            collector.GetSnapshot(
                category:
                    RuntimeDiagnosticCategory.RuntimeAttachment);

        Assert.Collection(
            records,
            record =>
            {
                Assert.Equal(
                    "EndpointPublished",
                    record.EventName);

                Assert.Equal(
                    "endpoint-01",
                    record.EndpointId);
            },
            record =>
                Assert.Equal(
                    "EndpointRemoved",
                    record.EventName));
    }

    [Fact]
    public void ConnectionLifecycle_PublishesStableOrderedDiagnostics()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                20);

        RuntimeContext context =
            new(
                new RuntimeDiagnosticPublisher(
                    collector));

        RuntimeEndpoint endpoint =
            context.AddEndpoint(
                CreateDescriptor());

        collector.Clear();

        endpoint.UpdateConnectionStatus(
            CreateStatus(
                EndpointConnectionState.Connecting));

        endpoint.UpdateConnectionStatus(
            CreateStatus(
                EndpointConnectionState.Synchronizing));

        endpoint.UpdateConnectionStatus(
            CreateStatus(
                EndpointConnectionState.Ready));

        IReadOnlyList<RuntimeDiagnosticRecord> records =
            collector.GetSnapshot();

        Assert.Equal(
            [
                "ConnectionStateChanged",
                "AttachmentStarted",
                "ConnectionStateChanged",
                "SynchronizationStarted",
                "ConnectionStateChanged",
                "SynchronizationCompleted",
                "AttachmentReady"
            ],
            records
                .Select(
                    record =>
                        record.EventName)
                .ToArray());

        RuntimeDiagnosticRecord firstTransition =
            records[0];

        Assert.Equal(
            "Disconnected",
            firstTransition.Details["PreviousState"]);

        Assert.Equal(
            "Connecting",
            firstTransition.Details["CurrentState"]);

        Assert.All(
            records,
            record =>
                Assert.Equal(
                    "endpoint-01",
                    record.EndpointId));
    }

    [Fact]
    public void SuccessfulRecovery_PublishesRecoveryOutcome()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                20);

        RuntimeEndpoint endpoint =
            new RuntimeContext(
                new RuntimeDiagnosticPublisher(
                    collector))
                .AddEndpoint(
                    CreateDescriptor());

        collector.Clear();

        endpoint.UpdateConnectionStatus(
            CreateStatus(
                EndpointConnectionState.Reconnecting));

        endpoint.UpdateConnectionStatus(
            CreateStatus(
                EndpointConnectionState.Synchronizing));

        endpoint.UpdateConnectionStatus(
            CreateStatus(
                EndpointConnectionState.Ready));

        RuntimeDiagnosticRecord recovery =
            Assert.Single(
                collector
                    .GetSnapshot(
                        category:
                            RuntimeDiagnosticCategory.RuntimeRecovery)
                    .Where(
                        record =>
                            record.EventName
                            == "RecoveryCompleted"));

        Assert.Equal(
            RuntimeDiagnosticOutcome.Succeeded,
            recovery.Outcome);
    }

    [Fact]
    public void FailedRecovery_PublishesSafeFailureWithoutStatusDetail()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                20);

        RuntimeEndpoint endpoint =
            new RuntimeContext(
                new RuntimeDiagnosticPublisher(
                    collector))
                .AddEndpoint(
                    CreateDescriptor());

        collector.Clear();

        endpoint.UpdateConnectionStatus(
            CreateStatus(
                EndpointConnectionState.Reconnecting));

        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Faulted,
                Timestamp,
                "Private address 100.x.x.x and COM10 must not escape."));

        RuntimeDiagnosticRecord recovery =
            Assert.Single(
                collector
                    .GetSnapshot(
                        category:
                            RuntimeDiagnosticCategory.RuntimeRecovery)
                    .Where(
                        record =>
                            record.EventName
                            == "RecoveryCompleted"));

        Assert.Equal(
            RuntimeDiagnosticOutcome.Failed,
            recovery.Outcome);

        Assert.DoesNotContain(
            collector.GetSnapshot(),
            record =>
                record.Details.Values.Any(
                    value =>
                        value.Contains(
                            "100.",
                            StringComparison.Ordinal)
                        || value.Contains(
                            "COM10",
                            StringComparison.Ordinal)));
    }

    [Fact]
    public void ThrowingDiagnosticSink_DoesNotInterruptRuntimeTransition()
    {
        RuntimeEndpoint endpoint =
            new RuntimeContext(
                new RuntimeDiagnosticPublisher(
                    new ThrowingSink()))
                .AddEndpoint(
                    CreateDescriptor());

        Exception? exception =
            Record.Exception(
                () => endpoint.UpdateConnectionStatus(
                    CreateStatus(
                        EndpointConnectionState.Connecting)));

        Assert.Null(
            exception);

        Assert.Equal(
            EndpointConnectionState.Connecting,
            endpoint.ConnectionStatus.State);
    }

    [Fact]
    public void DefaultContext_PreservesExistingRuntimeBehavior()
    {
        RuntimeContext context =
            new();

        RuntimeEndpoint endpoint =
            context.AddEndpoint(
                CreateDescriptor());

        endpoint.UpdateConnectionStatus(
            CreateStatus(
                EndpointConnectionState.Ready));

        Assert.Equal(
            EndpointConnectionState.Ready,
            endpoint.ConnectionStatus.State);
    }

    private static EndpointDescriptor CreateDescriptor()
    {
        return new EndpointDescriptor(
            new EndpointId(
                "endpoint-01"));
    }

    private static EndpointConnectionStatus CreateStatus(
        EndpointConnectionState state)
    {
        return new EndpointConnectionStatus(
            state,
            Timestamp);
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
                "Test diagnostic failure.");
        }
    }
}
