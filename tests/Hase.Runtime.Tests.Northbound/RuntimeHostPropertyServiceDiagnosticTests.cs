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

public sealed class RuntimeHostPropertyServiceDiagnosticTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "instrument-one");

    private static readonly PropertyId PropertyId =
        new(
            "property-one");

    [Fact]
    public void GetCached_DoesNotPublishInteractionDiagnostics()
    {
        TestFixture fixture =
            new();

        _ =
            fixture.Service.GetCached(
                fixture.Target);

        Assert.Empty(
            fixture.Collector.GetSnapshot());
    }

    [Fact]
    public async Task ReadAsync_PublishesCorrelatedStructuralDiagnostics()
    {
        TestFixture fixture =
            new();

        RuntimeHostPropertyOperationResult result =
            await fixture.Service.ReadAsync(
                fixture.Target);

        Assert.True(
            result.IsSuccess);

        IReadOnlyList<RuntimeDiagnosticRecord> records =
            fixture.Collector.GetSnapshot();

        Assert.Equal(
            [
                "PropertyReadStarted",
                "PropertyReadCompleted"
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
            "instrument-one",
            records[0].Details["instrument"]);
        Assert.Equal(
            "property-one",
            records[0].Details["path"]);
        Assert.Equal(
            RuntimeDiagnosticOutcome.Succeeded,
            records[1].Outcome);
        Assert.NotNull(
            records[1].Duration);
    }

    [Fact]
    public async Task WriteAsync_DoesNotPublishRequestedOrConfirmedValues()
    {
        TestFixture fixture =
            new();

        RuntimeHostPropertyOperationResult result =
            await fixture.Service.WriteAsync(
                fixture.Target,
                true);

        Assert.True(
            result.IsSuccess);

        IReadOnlyList<RuntimeDiagnosticRecord> records =
            fixture.Collector.GetSnapshot();

        Assert.Equal(
            [
                "PropertyWriteStarted",
                "PropertyWriteCompleted"
            ],
            records
                .Select(
                    record =>
                        record.EventName)
                .ToArray());

        Assert.All(
            records,
            record =>
            {
                Assert.Equal(
                    2,
                    record.Details.Count);

                Assert.DoesNotContain(
                    record.Details,
                    detail =>
                        string.Equals(
                            detail.Value,
                            bool.TrueString,
                            StringComparison.OrdinalIgnoreCase));
            });
    }

    [Fact]
    public async Task ReadAsync_NormalizedTimeoutPublishesTimedOutFailure()
    {
        TestFixture fixture =
            new(
                EndpointAttachmentPropertyOperationResult.Failed(
                    EndpointAttachmentPropertyOperationStatus.TimedOut,
                    "sensitive endpoint diagnostic"));

        RuntimeHostPropertyOperationResult result =
            await fixture.Service.ReadAsync(
                fixture.Target);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.TimedOut,
            result.Status);

        RuntimeDiagnosticRecord failed =
            fixture.Collector.GetSnapshot()[1];

        Assert.Equal(
            "PropertyReadFailed",
            failed.EventName);
        Assert.Equal(
            RuntimeDiagnosticOutcome.TimedOut,
            failed.Outcome);
        Assert.DoesNotContain(
            failed.Details,
            detail =>
                detail.Value.Contains(
                    "sensitive",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteAsync_NormalizedFailurePublishesFailedOutcome()
    {
        TestFixture fixture =
            new(
                EndpointAttachmentPropertyOperationResult.Failed(
                    EndpointAttachmentPropertyOperationStatus.Rejected));

        RuntimeHostPropertyOperationResult result =
            await fixture.Service.WriteAsync(
                fixture.Target,
                true);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.EndpointRejected,
            result.Status);
        Assert.Equal(
            RuntimeDiagnosticOutcome.Failed,
            fixture.Collector.GetSnapshot()[1].Outcome);
    }

    [Fact]
    public async Task ReadAsync_PreCancelledPublishesCancelledFailure()
    {
        TestFixture fixture =
            new();

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                fixture.Service.ReadAsync(
                    fixture.Target,
                    cancellationSource.Token));

        RuntimeDiagnosticRecord failed =
            fixture.Collector.GetSnapshot()[1];

        Assert.Equal(
            "PropertyReadFailed",
            failed.EventName);
        Assert.Equal(
            RuntimeDiagnosticOutcome.Cancelled,
            failed.Outcome);
        Assert.Equal(
            0,
            fixture.PropertyOperations.ReadCallCount);
    }

    private sealed class TestFixture
    {
        public TestFixture(
            EndpointAttachmentPropertyOperationResult? operationResult = null)
        {
            Collector =
                new BoundedRuntimeDiagnosticCollector(
                    10);

            PropertyOperations =
                new TestPropertyOperations(
                    operationResult
                    ?? EndpointAttachmentPropertyOperationResult.Successful(
                        new PropertyValue(
                            true,
                            DateTimeOffset.UnixEpoch)));

            RuntimeEndpointAttachmentInventoryEntry entry =
                CreateEntry(
                    PropertyOperations);

            var projection =
                new RuntimeHostAttachmentProjection(
                    new TestAttachmentInventory(
                        entry));

            RuntimeHostPublishedAttachment attachment =
                Assert.Single(
                    projection.List());

            Target =
                new RuntimeHostPropertyTarget(
                    entry.EndpointId,
                    attachment.Generation,
                    InstrumentId,
                    PropertyId);

            Service =
                new RuntimeHostPropertyService(
                    projection,
                    new RuntimeDiagnosticPublisher(
                        Collector));
        }

        public BoundedRuntimeDiagnosticCollector Collector
        {
            get;
        }

        public TestPropertyOperations PropertyOperations
        {
            get;
        }

        public RuntimeHostPropertyTarget Target
        {
            get;
        }

        public RuntimeHostPropertyService Service
        {
            get;
        }
    }

    private static RuntimeEndpointAttachmentInventoryEntry CreateEntry(
        TestPropertyOperations propertyOperations)
    {
        var propertyDescriptor =
            new PropertyDescriptor(
                PropertyId,
                new DescriptorPath(
                    "Instrument",
                    "Property"),
                "Property",
                new BooleanDataDescriptor())
            {
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };

        var instrumentDescriptor =
            new InstrumentDescriptor(
                InstrumentId,
                "Instrument",
                new InstrumentKind(
                    "test"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            propertyDescriptor
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
                propertyOperations));
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
            IEndpointAttachmentPropertyOperations propertyOperations)
        {
            RuntimeEndpoint =
                runtimeEndpoint;

            PropertyOperations =
                propertyOperations;

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

        public IEndpointAttachmentPropertyOperations PropertyOperations
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

    public sealed class TestPropertyOperations
        : IEndpointAttachmentPropertyOperations
    {
        private readonly EndpointAttachmentPropertyOperationResult
            operationResult;

        public TestPropertyOperations(
            EndpointAttachmentPropertyOperationResult operationResult)
        {
            this.operationResult =
                operationResult;
        }

        public int ReadCallCount
        {
            get;
            private set;
        }

        public Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            CancellationToken cancellationToken = default)
        {
            ReadCallCount++;

            return Task.FromResult(
                operationResult);
        }

        public Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                operationResult);
        }
    }
}
