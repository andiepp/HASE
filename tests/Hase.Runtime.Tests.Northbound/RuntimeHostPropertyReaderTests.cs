using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostPropertyReaderTests
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
            () => new RuntimeHostPropertyReader(
                null!));
    }

    [Fact]
    public async Task ReadAsync_NullTarget_Throws()
    {
        TestFixture fixture =
            CreateFixture();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => fixture.Reader.ReadAsync(
                null!));
    }

    [Fact]
    public async Task ReadAsync_MissingAttachment_ReturnsNotCurrent()
    {
        TestFixture fixture =
            CreateFixture();

        fixture.Inventory.SetEntries();

        RuntimeHostPropertyOperationResult result =
            await fixture.Reader.ReadAsync(
                fixture.Target);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.AttachmentNotCurrent,
            result.Status);

        Assert.Equal(
            0,
            fixture.PropertyOperations.ReadCallCount);
    }

    [Fact]
    public async Task ReadAsync_DifferentGeneration_ReturnsNotCurrent()
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
            await fixture.Reader.ReadAsync(
                target);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.AttachmentNotCurrent,
            result.Status);

        Assert.Equal(
            0,
            fixture.PropertyOperations.ReadCallCount);
    }

    [Fact]
    public async Task ReadAsync_MissingInstrument_ReturnsInstrumentNotFound()
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
            await fixture.Reader.ReadAsync(
                target);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.InstrumentNotFound,
            result.Status);
    }

    [Fact]
    public async Task ReadAsync_MissingProperty_ReturnsPropertyNotFound()
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
            await fixture.Reader.ReadAsync(
                target);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.PropertyNotFound,
            result.Status);
    }

    [Fact]
    public async Task ReadAsync_NonReadableProperty_ReturnsReadNotSupported()
    {
        TestFixture fixture =
            CreateFixture(
                PropertyAccessMode.Write);

        RuntimeHostPropertyOperationResult result =
            await fixture.Reader.ReadAsync(
                fixture.Target);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.ReadNotSupported,
            result.Status);

        Assert.Equal(
            0,
            fixture.PropertyOperations.ReadCallCount);
    }

    [Fact]
    public async Task ReadAsync_Success_ReturnsConfirmedValueAndUsesLogicalTarget()
    {
        PropertyValue confirmedValue =
            new(
                "confirmed",
                DateTimeOffset.UnixEpoch);

        TestFixture fixture =
            CreateFixture();

        fixture.PropertyOperations.ReadImplementation =
            (
                instrumentId,
                propertyId,
                cancellationToken) =>
            {
                Assert.Equal(
                    InstrumentId,
                    instrumentId);

                Assert.Equal(
                    PropertyId,
                    propertyId);

                GetRuntimeProperty(
                        fixture.Entry)
                    .UpdateValue(
                        confirmedValue);

                return Task.FromResult(
                    EndpointAttachmentPropertyOperationResult.Successful(
                        confirmedValue));
            };

        RuntimeHostPropertyOperationResult result =
            await fixture.Reader.ReadAsync(
                fixture.Target);

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
        RuntimeHostPropertyOperationStatus.ReadNotSupported)]
    [InlineData(
        EndpointAttachmentPropertyOperationStatus.InvalidValue,
        RuntimeHostPropertyOperationStatus.EndpointFailure)]
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
    public async Task ReadAsync_AttachmentFailure_MapsStatus(
        EndpointAttachmentPropertyOperationStatus attachmentStatus,
        RuntimeHostPropertyOperationStatus expectedStatus)
    {
        TestFixture fixture =
            CreateFixture();

        fixture.PropertyOperations.ReadImplementation =
            (
                instrumentId,
                propertyId,
                cancellationToken) =>
                    Task.FromResult(
                        EndpointAttachmentPropertyOperationResult.Failed(
                            attachmentStatus,
                            "safe diagnostic"));

        RuntimeHostPropertyOperationResult result =
            await fixture.Reader.ReadAsync(
                fixture.Target);

        Assert.Equal(
            expectedStatus,
            result.Status);

        Assert.Equal(
            "safe diagnostic",
            result.Diagnostic);
    }

    [Fact]
    public async Task ReadAsync_CallerCancellation_Throws()
    {
        TestFixture fixture =
            CreateFixture();

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Reader.ReadAsync(
                fixture.Target,
                cancellationSource.Token));

        Assert.Equal(
            0,
            fixture.PropertyOperations.ReadCallCount);
    }

    [Fact]
    public async Task ReadAsync_DetachRace_DoesNotRouteToReplacementAttachment()
    {
        TestFixture fixture =
            CreateFixture();

        PropertyValue confirmedValue =
            new(
                "old attachment",
                DateTimeOffset.UnixEpoch);

        var replacementOperations =
            new TestPropertyOperations();

        RuntimeEndpointAttachmentInventoryEntry replacementEntry =
            CreateEntry(
                replacementOperations,
                PropertyAccessMode.Read);

        fixture.PropertyOperations.ReadImplementation =
            (
                instrumentId,
                propertyId,
                cancellationToken) =>
            {
                fixture.Inventory.SetEntries(
                    replacementEntry);

                return Task.FromResult(
                    EndpointAttachmentPropertyOperationResult.Successful(
                        confirmedValue));
            };

        RuntimeHostPropertyOperationResult result =
            await fixture.Reader.ReadAsync(
                fixture.Target);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            1,
            fixture.PropertyOperations.ReadCallCount);

        Assert.Equal(
            0,
            replacementOperations.ReadCallCount);
    }

    private static TestFixture CreateFixture(
        PropertyAccessMode accessMode = PropertyAccessMode.Read)
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
            new RuntimeHostPropertyReader(
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
                new StringDataDescriptor())
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
        RuntimeEndpointAttachmentInventoryEntry? fixture)
    {
        return fixture?
            .RuntimeEndpoint
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
        RuntimeHostPropertyReader Reader);

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
            CancellationToken,
            Task<EndpointAttachmentPropertyOperationResult>>?
            ReadImplementation
        {
            get;
            set;
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

            return ReadImplementation?.Invoke(
                    instrumentId,
                    propertyId,
                    cancellationToken)
                ?? Task.FromResult(
                    EndpointAttachmentPropertyOperationResult.Failed(
                        EndpointAttachmentPropertyOperationStatus.Failure));
        }

        public Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}