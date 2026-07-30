using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostAttachmentProjectionDiagnosticTests
{
    private static readonly DateTimeOffset EndedAtUtc =
        new(
            2026,
            7,
            30,
            16,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Publication_PublishesOneGenerationQualifiedRecord()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                10);

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "endpoint-01",
                collector);

        using var projection =
            new RuntimeHostAttachmentProjection(
                new EmptyAttachmentInventory());

        projection.OnAttachmentPublished(
            new RuntimeEndpointAttachmentPublished(
                entry));

        RuntimeDiagnosticRecord record =
            Assert.Single(
                collector.GetSnapshot());

        Assert.Equal(
            "AttachmentPublished",
            record.EventName);

        Assert.Equal(
            "endpoint-01",
            record.EndpointId);

        Assert.NotNull(
            record.AttachmentGeneration);

        Assert.NotEqual(
            Guid.Empty,
            record.AttachmentGeneration);
    }

    [Fact]
    public void RepeatedPublication_DoesNotDuplicateRecord()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                10);

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "endpoint-01",
                collector);

        using var projection =
            new RuntimeHostAttachmentProjection(
                new EmptyAttachmentInventory());

        RuntimeEndpointAttachmentPublished publication =
            new(
                entry);

        projection.OnAttachmentPublished(
            publication);

        projection.OnAttachmentPublished(
            publication);

        Assert.Single(
            collector.GetSnapshot());
    }

    [Fact]
    public void Ending_PublishesSameGenerationExactlyOnce()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                10);

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "endpoint-01",
                collector);

        using var projection =
            new RuntimeHostAttachmentProjection(
                new EmptyAttachmentInventory());

        projection.OnAttachmentPublished(
            new RuntimeEndpointAttachmentPublished(
                entry));

        RuntimeEndpointAttachmentEnded ending =
            new(
                entry,
                EndedAtUtc);

        projection.OnAttachmentEnded(
            ending);

        projection.OnAttachmentEnded(
            ending);

        IReadOnlyList<RuntimeDiagnosticRecord> records =
            collector.GetSnapshot();

        Assert.Equal(
            2,
            records.Count);

        Assert.Equal(
            "AttachmentEnded",
            records[1].EventName);

        Assert.Equal(
            records[0].AttachmentGeneration,
            records[1].AttachmentGeneration);
    }

    [Fact]
    public void Replacement_UsesNewGenerationAndStableEventOrder()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                10);

        RuntimeContext context =
            new(
                new RuntimeDiagnosticPublisher(
                    collector));

        RuntimeEndpointAttachmentInventoryEntry firstEntry =
            CreateEntry(
                "endpoint-01",
                context);

        RuntimeEndpointAttachmentInventoryEntry secondEntry =
            CreateEntry(
                "endpoint-01",
                context);

        using var projection =
            new RuntimeHostAttachmentProjection(
                new EmptyAttachmentInventory());

        projection.OnAttachmentPublished(
            new RuntimeEndpointAttachmentPublished(
                firstEntry));

        projection.OnAttachmentEnded(
            new RuntimeEndpointAttachmentEnded(
                firstEntry,
                EndedAtUtc));

        projection.OnAttachmentPublished(
            new RuntimeEndpointAttachmentPublished(
                secondEntry));

        IReadOnlyList<RuntimeDiagnosticRecord> records =
            collector.GetSnapshot();

        Assert.Equal(
            [
                "AttachmentPublished",
                "AttachmentEnded",
                "AttachmentPublished"
            ],
            records
                .Select(
                    record =>
                        record.EventName)
                .ToArray());

        Assert.Equal(
            records[0].AttachmentGeneration,
            records[1].AttachmentGeneration);

        Assert.NotEqual(
            records[0].AttachmentGeneration,
            records[2].AttachmentGeneration);
    }

    [Fact]
    public void ThrowingSink_DoesNotInterruptProjectionChanges()
    {
        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "endpoint-01",
                new ThrowingSink());

        using var projection =
            new RuntimeHostAttachmentProjection(
                new EmptyAttachmentInventory());

        Exception? exception =
            Record.Exception(
                () => projection.OnAttachmentPublished(
                    new RuntimeEndpointAttachmentPublished(
                        entry)));

        Assert.Null(
            exception);
    }

    private static RuntimeEndpointAttachmentInventoryEntry CreateEntry(
        string endpointId,
        IRuntimeDiagnosticSink sink)
    {
        RuntimeContext context =
            new(
                new RuntimeDiagnosticPublisher(
                    sink));

        return CreateEntry(
            endpointId,
            context);
    }

    private static RuntimeEndpointAttachmentInventoryEntry CreateEntry(
        string endpointId,
        RuntimeContext context)
    {
        RuntimeEndpoint endpoint =
            new(
                context,
                new EndpointDescriptor(
                    new EndpointId(
                        endpointId)));

        return new RuntimeEndpointAttachmentInventoryEntry(
            new TestEndpointAttachmentSession(
                endpoint));
    }

    private sealed class EmptyAttachmentInventory :
        IRuntimeEndpointAttachmentInventory
    {
        public Task<RuntimeEndpointAttachmentInventoryEntry> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public RuntimeEndpointAttachmentInventoryEntry? Find(
            EndpointId endpointId)
        {
            return null;
        }

        public IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> List()
        {
            return [];
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

    private sealed class TestEndpointAttachmentSession :
        IEndpointAttachmentSession
    {
        public TestEndpointAttachmentSession(
            RuntimeEndpoint runtimeEndpoint)
        {
            RuntimeEndpoint =
                runtimeEndpoint;

            Request =
                null!;
        }

        public EndpointAttachmentRequest Request { get; }

        public RuntimeEndpoint RuntimeEndpoint { get; }

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
                "Test observer failure.");
        }
    }
}
