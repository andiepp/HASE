using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostCommandServiceDiagnosticTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-one");

    private static readonly DescriptorPath CommandPath =
        new(
            "Controller",
            "ToggleLed");

    [Fact]
    public async Task ExecuteAsync_PublishesCorrelatedStructuralDiagnostics()
    {
        TestFixture fixture =
            new(
                EndpointAttachmentCommandOperationResult.Successful());

        RuntimeHostCommandOperationResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Target,
                argument: null);

        Assert.True(
            result.IsSuccess);

        IReadOnlyList<RuntimeDiagnosticRecord> records =
            fixture.Collector.GetSnapshot();

        Assert.Equal(
            [
                "CommandExecutionStarted",
                "CommandExecutionCompleted"
            ],
            records
                .Select(
                    record =>
                        record.EventName)
                .ToArray());

        Assert.Equal(
            records[0].OperationId,
            records[1].OperationId);
        Assert.Equal(
            "endpoint-one",
            records[0].EndpointId);
        Assert.Equal(
            fixture.Target.AttachmentGeneration.Value,
            records[0].AttachmentGeneration);
        Assert.Equal(
            "controller-one",
            records[0].Details["instrument"]);
        Assert.Equal(
            "Controller.ToggleLed",
            records[0].Details["path"]);
        Assert.Equal(
            RuntimeDiagnosticOutcome.Succeeded,
            records[1].Outcome);
        Assert.NotNull(
            records[1].Duration);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotPublishArgumentOrReturnValue()
    {
        ByteArrayValue argument =
            new(
                new byte[]
                {
                    0x01,
                    0xA5
                });

        ByteArrayValue returnValue =
            new(
                new byte[]
                {
                    0x02,
                    0x5A
                });

        TestFixture fixture =
            new(
                EndpointAttachmentCommandOperationResult.Successful(
                    returnValue),
                new CommandArgumentDescriptor(
                    "Payload",
                    new ByteArrayDataDescriptor()));

        RuntimeHostCommandOperationResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Target,
                argument);

        Assert.Same(
            returnValue,
            result.ReturnValue);

        Assert.All(
            fixture.Collector.GetSnapshot(),
            record =>
            {
                Assert.Equal(
                    2,
                    record.Details.Count);

                Assert.DoesNotContain(
                    record.Details.Values,
                    value =>
                        value.Contains(
                            "01A5",
                            StringComparison.OrdinalIgnoreCase)
                        || value.Contains(
                            "025A",
                            StringComparison.OrdinalIgnoreCase));
            });
    }

    [Fact]
    public async Task ExecuteAsync_NormalizedFailureDoesNotPublishDiagnosticText()
    {
        TestFixture fixture =
            new(
                EndpointAttachmentCommandOperationResult.Failed(
                    EndpointAttachmentCommandOperationStatus.Rejected,
                    "sensitive endpoint diagnostic"));

        RuntimeHostCommandOperationResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Target,
                argument: null);

        Assert.Equal(
            RuntimeHostCommandOperationStatus.EndpointRejected,
            result.Status);

        RuntimeDiagnosticRecord failed =
            fixture.Collector.GetSnapshot()[1];

        Assert.Equal(
            "CommandExecutionFailed",
            failed.EventName);
        Assert.Equal(
            RuntimeDiagnosticOutcome.Failed,
            failed.Outcome);
        Assert.DoesNotContain(
            failed.Details,
            detail =>
                detail.Value.Contains(
                    "sensitive",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_NormalizedTimeoutPublishesTimedOutOutcome()
    {
        TestFixture fixture =
            new(
                EndpointAttachmentCommandOperationResult.Failed(
                    EndpointAttachmentCommandOperationStatus.TimedOut));

        RuntimeHostCommandOperationResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Target,
                argument: null);

        Assert.Equal(
            RuntimeHostCommandOperationStatus.TimedOut,
            result.Status);
        Assert.Equal(
            RuntimeDiagnosticOutcome.TimedOut,
            fixture.Collector.GetSnapshot()[1].Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelledPublishesCancelledOutcome()
    {
        TestFixture fixture =
            new(
                EndpointAttachmentCommandOperationResult.Successful());

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                fixture.Service.ExecuteAsync(
                    fixture.Target,
                    argument: null,
                    cancellationSource.Token));

        RuntimeDiagnosticRecord failed =
            fixture.Collector.GetSnapshot()[1];

        Assert.Equal(
            "CommandExecutionFailed",
            failed.EventName);
        Assert.Equal(
            RuntimeDiagnosticOutcome.Cancelled,
            failed.Outcome);
        Assert.Equal(
            0,
            fixture.CommandOperations.ExecuteCallCount);
    }

    private sealed class TestFixture
    {
        public TestFixture(
            EndpointAttachmentCommandOperationResult operationResult,
            CommandArgumentDescriptor? argumentDescriptor = null)
        {
            Collector =
                new BoundedRuntimeDiagnosticCollector(
                    10);

            CommandOperations =
                new TestCommandOperations(
                    operationResult);

            RuntimeEndpointAttachmentInventoryEntry entry =
                CreateEntry(
                    CommandOperations,
                    argumentDescriptor);

            var projection =
                new RuntimeHostAttachmentProjection(
                    new TestAttachmentInventory(
                        entry));

            RuntimeHostPublishedAttachment attachment =
                Assert.Single(
                    projection.List());

            Target =
                new RuntimeHostCommandTarget(
                    entry.EndpointId,
                    attachment.Generation,
                    InstrumentId,
                    CommandPath);

            Service =
                new RuntimeHostCommandService(
                    projection,
                    new RuntimeDiagnosticPublisher(
                        Collector));
        }

        public BoundedRuntimeDiagnosticCollector Collector
        {
            get;
        }

        public TestCommandOperations CommandOperations
        {
            get;
        }

        public RuntimeHostCommandTarget Target
        {
            get;
        }

        public RuntimeHostCommandService Service
        {
            get;
        }
    }

    private static RuntimeEndpointAttachmentInventoryEntry CreateEntry(
        TestCommandOperations commandOperations,
        CommandArgumentDescriptor? argumentDescriptor)
    {
        CommandDescriptor commandDescriptor =
            argumentDescriptor is null
                ? new CommandDescriptor(
                    CommandPath,
                    "Toggle LED")
                : new CommandDescriptor(
                    CommandPath,
                    "Toggle LED",
                    argumentDescriptor);

        var instrumentDescriptor =
            new InstrumentDescriptor(
                InstrumentId,
                "Controller",
                new InstrumentKind(
                    "test"))
            {
                Interface =
                    new InstrumentInterface(
                        commands:
                        [
                            commandDescriptor
                        ])
            };

        var runtimeEndpoint =
            new RuntimeEndpoint(
                new RuntimeContext(),
                new EndpointDescriptor(
                    new EndpointId(
                        "endpoint-one"),
                    [
                        instrumentDescriptor
                    ]));

        return new RuntimeEndpointAttachmentInventoryEntry(
            new TestEndpointAttachmentSession(
                runtimeEndpoint,
                commandOperations));
    }

    private sealed class TestAttachmentInventory
        : IRuntimeEndpointAttachmentInventory
    {
        private readonly IReadOnlyList<
            RuntimeEndpointAttachmentInventoryEntry>
            entries;

        public TestAttachmentInventory(
            params RuntimeEndpointAttachmentInventoryEntry[] entries)
        {
            this.entries =
                entries.ToArray();
        }

        public Task<RuntimeEndpointAttachmentInventoryEntry> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public RuntimeEndpointAttachmentInventoryEntry? Find(
            EndpointId endpointId)
        {
            return entries.FirstOrDefault(
                entry =>
                    entry.EndpointId == endpointId);
        }

        public IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> List()
        {
            return entries.ToArray();
        }

        public Task<bool> DetachAsync(
            EndpointId endpointId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestEndpointAttachmentSession
        : IEndpointAttachmentSession
    {
        public TestEndpointAttachmentSession(
            RuntimeEndpoint runtimeEndpoint,
            IEndpointAttachmentCommandOperations commandOperations)
        {
            RuntimeEndpoint =
                runtimeEndpoint;

            CommandOperations =
                commandOperations;

            Request =
                null!;
        }

        public EndpointAttachmentRequest Request
        {
            get;
        }

        public RuntimeEndpoint RuntimeEndpoint
        {
            get;
        }

        public IEndpointAttachmentCommandOperations CommandOperations
        {
            get;
        }

        public Task ShutdownAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestCommandOperations
        : IEndpointAttachmentCommandOperations
    {
        private readonly EndpointAttachmentCommandOperationResult
            result;

        public TestCommandOperations(
            EndpointAttachmentCommandOperationResult result)
        {
            this.result =
                result;
        }

        public int ExecuteCallCount
        {
            get;
            private set;
        }

        public Task<EndpointAttachmentCommandOperationResult> ExecuteAsync(
            InstrumentId instrumentId,
            DescriptorPath commandPath,
            object? argument,
            CancellationToken cancellationToken = default)
        {
            ExecuteCallCount++;

            return Task.FromResult(
                result);
        }
    }
}
