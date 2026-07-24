using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostPropertyWriterTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "instrument-one");

    private static readonly PropertyId PropertyId =
        new(
            "property-one");

    [Fact]
    public void Constructor_NullProjection_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostPropertyWriter(
                null!));
    }

    [Fact]
    public async Task WriteAsync_NullTarget_Throws()
    {
        TestFixture fixture =
            CreateFixture();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => fixture.Writer.WriteAsync(
                null!,
                true));
    }

    [Fact]
    public async Task WriteAsync_MissingAttachment_ReturnsNotCurrent()
    {
        TestFixture fixture =
            CreateFixture();

        fixture.Inventory.SetEntries();

        RuntimeHostPropertyOperationResult result =
            await fixture.Writer.WriteAsync(
                fixture.Target,
                true);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.AttachmentNotCurrent,
            result.Status);

        Assert.Equal(
            0,
            fixture.PropertyOperations.WriteCallCount);
    }

    [Fact]
    public async Task WriteAsync_DifferentGeneration_ReturnsNotCurrent()
    {
        TestFixture fixture =
            CreateFixture();

        var target =
            new RuntimeHostPropertyTarget(
                fixture.Target.EndpointId,
                RuntimeEndpointAttachmentGeneration.CreateNew(),
                fixture.Target.InstrumentId,
                fixture.Target.PropertyId);

        RuntimeHostPropertyOperationResult result =
            await fixture.Writer.WriteAsync(
                target,
                true);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.AttachmentNotCurrent,
            result.Status);
    }

    [Fact]
    public async Task WriteAsync_MissingInstrument_ReturnsInstrumentNotFound()
    {
        TestFixture fixture =
            CreateFixture();

        var target =
            new RuntimeHostPropertyTarget(
                fixture.Target.EndpointId,
                fixture.Target.AttachmentGeneration,
                new InstrumentId(
                    "missing-instrument"),
                fixture.Target.PropertyId);

        RuntimeHostPropertyOperationResult result =
            await fixture.Writer.WriteAsync(
                target,
                true);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.InstrumentNotFound,
            result.Status);
    }

    [Fact]
    public async Task WriteAsync_MissingProperty_ReturnsPropertyNotFound()
    {
        TestFixture fixture =
            CreateFixture();

        var target =
            new RuntimeHostPropertyTarget(
                fixture.Target.EndpointId,
                fixture.Target.AttachmentGeneration,
                fixture.Target.InstrumentId,
                new PropertyId(
                    "missing-property"));

        RuntimeHostPropertyOperationResult result =
            await fixture.Writer.WriteAsync(
                target,
                true);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.PropertyNotFound,
            result.Status);
    }

    [Fact]
    public async Task WriteAsync_NonWritableProperty_ReturnsWriteNotSupported()
    {
        TestFixture fixture =
            CreateFixture(
                PropertyAccessMode.Read);

        RuntimeHostPropertyOperationResult result =
            await fixture.Writer.WriteAsync(
                fixture.Target,
                true);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.WriteNotSupported,
            result.Status);

        Assert.Equal(
            0,
            fixture.PropertyOperations.WriteCallCount);
    }

    [Fact]
    public async Task WriteAsync_InvalidValue_ReturnsInvalidWithoutSubmitting()
    {
        TestFixture fixture =
            CreateFixture();

        RuntimeHostPropertyOperationResult result =
            await fixture.Writer.WriteAsync(
                fixture.Target,
                "not a Boolean");

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.InvalidValue,
            result.Status);

        Assert.Equal(
            0,
            fixture.PropertyOperations.WriteCallCount);

        Assert.Null(
            GetRuntimeProperty(
                    fixture.Entry)
                .CurrentValue);
    }

    [Fact]
    public async Task WriteAsync_Success_ReturnsConfirmedValueAndUsesLogicalTarget()
    {
        TestFixture fixture =
            CreateFixture();

        PropertyValue confirmedValue =
            new(
                true,
                DateTimeOffset.UnixEpoch);

        fixture.PropertyOperations.WriteImplementation =
            (
                instrumentId,
                propertyId,
                requestedValue,
                cancellationToken) =>
            {
                Assert.Equal(
                    InstrumentId,
                    instrumentId);

                Assert.Equal(
                    PropertyId,
                    propertyId);

                Assert.Equal(
                    true,
                    requestedValue);

                GetRuntimeProperty(
                        fixture.Entry)
                    .UpdateValue(
                        confirmedValue);

                return Task.FromResult(
                    EndpointAttachmentPropertyOperationResult.Successful(
                        confirmedValue));
            };

        RuntimeHostPropertyOperationResult result =
            await fixture.Writer.WriteAsync(
                fixture.Target,
                true);

        Assert.True(
            result.IsSuccess);

        Assert.Same(
            confirmedValue,
            result.ConfirmedValue);

        Assert.Same(
            confirmedValue,
            GetRuntimeProperty(
                    fixture.Entry)
                .CurrentValue);
    }

    [Theory]
    [InlineData(
        EndpointAttachmentPropertyOperationStatus.NotSupported,
        RuntimeHostPropertyOperationStatus.WriteNotSupported)]
    [InlineData(
        EndpointAttachmentPropertyOperationStatus.InvalidValue,
        RuntimeHostPropertyOperationStatus.InvalidValue)]
    [InlineData(
        EndpointAttachmentPropertyOperationStatus.Rejected,
        RuntimeHostPropertyOperationStatus.EndpointRejected)]
    [InlineData(
        EndpointAttachmentPropertyOperationStatus.Failure,
        RuntimeHostPropertyOperationStatus.EndpointFailure)]
    [InlineData(
        EndpointAttachmentPropertyOperationStatus.Unavailable,
        RuntimeHostPropertyOperationStatus.EndpointUnavailable)]
    [InlineData(
        EndpointAttachmentPropertyOperationStatus.TimedOut,
        RuntimeHostPropertyOperationStatus.TimedOut)]
    public async Task WriteAsync_AttachmentFailure_MapsStatusWithoutCacheMutation(
        EndpointAttachmentPropertyOperationStatus attachmentStatus,
        RuntimeHostPropertyOperationStatus expectedStatus)
    {
        TestFixture fixture =
            CreateFixture();

        PropertyValue originalValue =
            new(
                false,
                DateTimeOffset.UnixEpoch);

        GetRuntimeProperty(
                fixture.Entry)
            .UpdateValue(
                originalValue);

        fixture.PropertyOperations.WriteImplementation =
            (
                instrumentId,
                propertyId,
                requestedValue,
                cancellationToken) =>
                    Task.FromResult(
                        EndpointAttachmentPropertyOperationResult.Failed(
                            attachmentStatus,
                            "safe diagnostic"));

        RuntimeHostPropertyOperationResult result =
            await fixture.Writer.WriteAsync(
                fixture.Target,
                true);

        Assert.Equal(
            expectedStatus,
            result.Status);

        Assert.Equal(
            "safe diagnostic",
            result.Diagnostic);

        Assert.Same(
            originalValue,
            GetRuntimeProperty(
                    fixture.Entry)
                .CurrentValue);
    }

    [Fact]
    public async Task WriteAsync_CallerCancellation_ThrowsWithoutSubmitting()
    {
        TestFixture fixture =
            CreateFixture();

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Writer.WriteAsync(
                fixture.Target,
                true,
                cancellationSource.Token));

        Assert.Equal(
            0,
            fixture.PropertyOperations.WriteCallCount);
    }

    [Fact]
    public async Task WriteAsync_DetachRace_DoesNotRouteToReplacementAttachment()
    {
        TestFixture fixture =
            CreateFixture();

        PropertyValue confirmedValue =
            new(
                true,
                DateTimeOffset.UnixEpoch);

        var replacementOperations =
            new TestPropertyOperations();

        RuntimeEndpointAttachmentInventoryEntry replacementEntry =
            CreateEntry(
                replacementOperations,
                PropertyAccessMode.Write);

        fixture.PropertyOperations.WriteImplementation =
            (
                instrumentId,
                propertyId,
                requestedValue,
                cancellationToken) =>
            {
                fixture.Inventory.SetEntries(
                    replacementEntry);

                return Task.FromResult(
                    EndpointAttachmentPropertyOperationResult.Successful(
                        confirmedValue));
            };

        RuntimeHostPropertyOperationResult result =
            await fixture.Writer.WriteAsync(
                fixture.Target,
                true);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            1,
            fixture.PropertyOperations.WriteCallCount);

        Assert.Equal(
            0,
            replacementOperations.WriteCallCount);
    }

    private static TestFixture CreateFixture(
        PropertyAccessMode accessMode = PropertyAccessMode.Write)
    {
        var propertyOperations =
            new TestPropertyOperations();

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                propertyOperations,
                accessMode);

        var inventory =
            new TestAttachmentInventory(
                entry);

        var projection =
            new RuntimeHostAttachmentProjection(
                inventory);

        RuntimeHostPublishedAttachment attachment =
            Assert.Single(
                projection.List());

        var target =
            new RuntimeHostPropertyTarget(
                entry.EndpointId,
                attachment.Generation,
                InstrumentId,
                PropertyId);

        return new TestFixture(
            inventory,
            entry,
            target,
            propertyOperations,
            new RuntimeHostPropertyWriter(
                projection));
    }

    private static RuntimeEndpointAttachmentInventoryEntry CreateEntry(
        TestPropertyOperations propertyOperations,
        PropertyAccessMode accessMode)
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
                    accessMode
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

    private static RuntimeProperty GetRuntimeProperty(
        RuntimeEndpointAttachmentInventoryEntry entry)
    {
        return entry.RuntimeEndpoint
            .FindInstrument(
                InstrumentId)
            ?.FindProperty(
                PropertyId)
            ?? throw new InvalidOperationException(
                "The test runtime Property was not found.");
    }

    private sealed record TestFixture(
        TestAttachmentInventory Inventory,
        RuntimeEndpointAttachmentInventoryEntry Entry,
        RuntimeHostPropertyTarget Target,
        TestPropertyOperations PropertyOperations,
        RuntimeHostPropertyWriter Writer);

    private sealed class TestAttachmentInventory
        : IRuntimeEndpointAttachmentInventory
    {
        private IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry>
            _entries;

        public TestAttachmentInventory(
            params RuntimeEndpointAttachmentInventoryEntry[] entries)
        {
            _entries =
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
            return _entries.FirstOrDefault(
                entry =>
                    entry.EndpointId == endpointId);
        }

        public IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> List()
        {
            return _entries.ToArray();
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

        public void SetEntries(
            params RuntimeEndpointAttachmentInventoryEntry[] entries)
        {
            _entries =
                entries.ToArray();
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

    private sealed class TestPropertyOperations
        : IEndpointAttachmentPropertyOperations
    {
        public Func<
            InstrumentId,
            PropertyId,
            object?,
            CancellationToken,
            Task<EndpointAttachmentPropertyOperationResult>>?
            WriteImplementation
        {
            get;
            set;
        }

        public int WriteCallCount
        {
            get;
            private set;
        }

        public Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            WriteCallCount++;

            return WriteImplementation?.Invoke(
                    instrumentId,
                    propertyId,
                    requestedValue,
                    cancellationToken)
                ?? Task.FromResult(
                    EndpointAttachmentPropertyOperationResult.Failed(
                        EndpointAttachmentPropertyOperationStatus.Failure));
        }
    }
}