using Hase.CompactProtocol;
using Hase.Runtime.Connections;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeHostObservationEndpointFamilyIntegrationTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-one");

    private static readonly PropertyId PropertyId =
        new(
            "controller.state");

    private static readonly DescriptorPath EventPath =
        new(
            "Controller",
            "ButtonPressed");

    [Fact]
    public async Task NativeAndCompactEndpoints_UseOneObservationService()
    {
        EndpointDescriptorDefinition descriptorDefinition =
            CreateDescriptorDefinition();

        RuntimeEndpointAttachmentInventoryEntry nativeEntry =
            CreateNativeEntry(
                descriptorDefinition);

        RuntimeEndpointAttachmentInventoryEntry compactEntry =
            CreateCompactEntry(
                descriptorDefinition);

        var inventory =
            new TestObservedInventory(
                nativeEntry,
                compactEntry);

        string identityDirectory =
            Path.Combine(
                Path.GetTempPath(),
                $"hase-observation-families-{Guid.NewGuid():N}");

        try
        {
            await using RuntimeHostNorthboundSnapshotComposition composition =
                await RuntimeHostNorthboundSnapshotComposition
                    .CreateFileBackedAsync(
                        inventory,
                        Path.Combine(
                            identityDirectory,
                            "runtime-host-identity.json"),
                        new RuntimeHostId(
                            "runtime-host-observation-families"));

            await using RuntimeHostObservationSubscription subscription =
                await composition.ObservationService.OpenSubscriptionAsync(
                    new RuntimeHostObservationSubscriptionOptions(
                        16));

            Assert.Equal(
                2,
                subscription.InitialSnapshot.Endpoints.Count);

            PublishedRuntimeEndpointSnapshot nativeSnapshot =
                FindSnapshot(
                    subscription,
                    nativeEntry.EndpointId);

            PublishedRuntimeEndpointSnapshot compactSnapshot =
                FindSnapshot(
                    subscription,
                    compactEntry.EndpointId);

            Assert.NotEqual(
                nativeSnapshot.Generation,
                compactSnapshot.Generation);

            RuntimeHostPropertyOperationResult nativeRead =
                await composition.PropertyService.ReadAsync(
                    CreateTarget(
                        nativeSnapshot));

            RuntimeHostPropertyOperationResult compactRead =
                await composition.PropertyService.ReadAsync(
                    CreateTarget(
                        compactSnapshot));

            Assert.True(
                nativeRead.IsSuccess);

            Assert.True(
                compactRead.IsSuccess);

            PublishEvent(
                nativeEntry,
                "native");

            PublishEvent(
                compactEntry,
                "compact");

            IReadOnlyList<RuntimeHostObservation> observations =
                await ReadAsync(
                    subscription,
                    4);

            RuntimeHostObservation nativeProperty =
                FindObservation(
                    observations,
                    nativeEntry.EndpointId,
                    RuntimeHostObservationKind.PropertyValueChanged);

            RuntimeHostObservation compactProperty =
                FindObservation(
                    observations,
                    compactEntry.EndpointId,
                    RuntimeHostObservationKind.PropertyValueChanged);

            RuntimeHostObservation nativeEvent =
                FindObservation(
                    observations,
                    nativeEntry.EndpointId,
                    RuntimeHostObservationKind.EventOccurred);

            RuntimeHostObservation compactEvent =
                FindObservation(
                    observations,
                    compactEntry.EndpointId,
                    RuntimeHostObservationKind.EventOccurred);

            Assert.Equal(
                nativeSnapshot.Generation,
                nativeProperty.AttachmentGeneration);

            Assert.Equal(
                compactSnapshot.Generation,
                compactProperty.AttachmentGeneration);

            Assert.Equal(
                nativeSnapshot.Generation,
                nativeEvent.AttachmentGeneration);

            Assert.Equal(
                compactSnapshot.Generation,
                compactEvent.AttachmentGeneration);

            Assert.Equal(
                "native",
                Assert.IsType<RuntimeHostEventOccurredObservationPayload>(
                        nativeEvent.Payload)
                    .Value);

            Assert.Equal(
                "compact",
                Assert.IsType<RuntimeHostEventOccurredObservationPayload>(
                        compactEvent.Payload)
                    .Value);

            await using RuntimeHostObservationSubscription laterSubscription =
                await composition.ObservationService.OpenSubscriptionAsync(
                    new RuntimeHostObservationSubscriptionOptions());

            await AssertNoObservationAsync(
                laterSubscription);

            inventory.End(
                nativeEntry);

            inventory.End(
                compactEntry);

            IReadOnlyList<RuntimeHostObservation> endings =
                await ReadAsync(
                    subscription,
                    2);

            Assert.All(
                endings,
                observation =>
                    Assert.Equal(
                        RuntimeHostObservationKind.AttachmentEnded,
                        observation.Kind));

            Assert.False(
                inventory.IsDisposed);
        }
        finally
        {
            if (Directory.Exists(
                    identityDirectory))
            {
                Directory.Delete(
                    identityDirectory,
                    recursive: true);
            }
        }
    }

    private static RuntimeEndpointAttachmentInventoryEntry CreateNativeEntry(
        EndpointDescriptorDefinition descriptorDefinition)
    {
        var endpointId =
            new EndpointId(
                "native-endpoint");

        RuntimeEndpoint endpoint =
            new RuntimeContext()
                .AddEndpoint(
                    descriptorDefinition.Materialize(
                        endpointId));

        var operations =
            new NativeEndpointAttachmentPropertyOperations(
                endpoint,
                TimeSpan.FromSeconds(
                    1),
                (
                    request,
                    timeout,
                    cancellationToken) =>
                {
                    var readRequest =
                        Assert.IsType<ReadPropertyRequest>(
                            request);

                    ProtocolMessage response =
                        new ReadPropertyResponse(
                            readRequest.CorrelationId,
                            ProtocolResult.Success,
                            CreatePropertyValue(
                                false));

                    return Task.FromResult(
                        response);
                });

        return CreateEntry(
            endpointId,
            endpoint,
            operations);
    }

    private static RuntimeEndpointAttachmentInventoryEntry CreateCompactEntry(
        EndpointDescriptorDefinition descriptorDefinition)
    {
        var endpointId =
            new EndpointId(
                "compact-endpoint");

        RuntimeEndpoint endpoint =
            new RuntimeContext()
                .AddEndpoint(
                    descriptorDefinition.Materialize(
                        endpointId));

        RuntimeProperty property =
            GetProperty(
                endpoint);

        var mapping =
            new CompactPropertyMapping(
                compactPropertyId: 0x01,
                InstrumentId,
                PropertyId,
                CompactPropertyValueEncoding.Boolean);

        var propertyMap =
            new CompactPropertyMap(
                descriptorDefinition,
                [
                    mapping
                ]);

        var operations =
            new CompactEndpointAttachmentPropertyOperations(
                propertyMap,
                (
                    compactPropertyId,
                    cancellationToken) =>
                {
                    property.UpdateValue(
                        CreatePropertyValue(
                            false));

                    return Task.FromResult(
                        new CompactRuntimePropertySynchronizationResult(
                            mapping,
                            property,
                            CompactPropertyReadStatus.Success));
                },
                (
                    compactPropertyId,
                    requestedValue,
                    cancellationToken) =>
                    throw new NotSupportedException());

        return CreateEntry(
            endpointId,
            endpoint,
            operations);
    }

    private static RuntimeEndpointAttachmentInventoryEntry CreateEntry(
        EndpointId endpointId,
        RuntimeEndpoint endpoint,
        IEndpointAttachmentPropertyOperations operations)
    {
        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready));

        var session =
            new EndpointAttachmentSession(
                new EndpointAttachmentRequest(
                    new StubConnectionDefinition(
                        endpointId),
                    HostRepositoryDescriptorSource.Instance),
                endpoint,
                operations,
                Array.Empty<IAsyncDisposable>());

        return new RuntimeEndpointAttachmentInventoryEntry(
            session);
    }

    private static EndpointDescriptorDefinition CreateDescriptorDefinition()
    {
        var property =
            new PropertyDescriptor(
                PropertyId,
                new DescriptorPath(
                    "Controller",
                    "State"),
                "Controller State",
                new BooleanDataDescriptor());

        var runtimeEvent =
            new EventDescriptor(
                EventPath,
                "Button pressed");

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            property
                        ],
                        events:
                        [
                            runtimeEvent
                        ])
            };

        return new EndpointDescriptorDefinition(
            instruments:
            [
                instrument
            ],
            metadata:
                new());
    }

    private static RuntimeHostPropertyTarget CreateTarget(
        PublishedRuntimeEndpointSnapshot snapshot)
    {
        return new RuntimeHostPropertyTarget(
            snapshot.EndpointId,
            snapshot.Generation,
            InstrumentId,
            PropertyId);
    }

    private static PublishedRuntimeEndpointSnapshot FindSnapshot(
        RuntimeHostObservationSubscription subscription,
        EndpointId endpointId)
    {
        return Assert.Single(
            subscription.InitialSnapshot.Endpoints.Where(
                endpoint =>
                    endpoint.EndpointId == endpointId));
    }

    private static RuntimeHostObservation FindObservation(
        IReadOnlyList<RuntimeHostObservation> observations,
        EndpointId endpointId,
        RuntimeHostObservationKind kind)
    {
        return Assert.Single(
            observations.Where(
                observation =>
                    observation.EndpointId == endpointId
                    && observation.Kind == kind));
    }

    private static RuntimeProperty GetProperty(
        RuntimeEndpoint endpoint)
    {
        return endpoint.FindInstrument(
                InstrumentId)
            ?.FindProperty(
                PropertyId)
            ?? throw new InvalidOperationException();
    }

    private static void PublishEvent(
        RuntimeEndpointAttachmentInventoryEntry entry,
        object? value)
    {
        RuntimeEvent runtimeEvent =
            entry.RuntimeEndpoint.FindInstrument(
                    InstrumentId)
                ?.FindEvent(
                    EventPath)
            ?? throw new InvalidOperationException();

        runtimeEvent.PublishOccurrence(
            DateTimeOffset.UtcNow,
            value);
    }

    private static PropertyValue CreatePropertyValue(
        bool value)
    {
        return new PropertyValue(
            value,
            DateTimeOffset.UtcNow);
    }

    private static async Task<IReadOnlyList<RuntimeHostObservation>> ReadAsync(
        RuntimeHostObservationSubscription subscription,
        int count)
    {
        using var cancellationSource =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(
                    3));

        var observations =
            new List<RuntimeHostObservation>();

        await foreach (
            RuntimeHostObservation observation
            in subscription.ReadAllAsync(
                cancellationSource.Token))
        {
            observations.Add(
                observation);

            if (observations.Count == count)
            {
                break;
            }
        }

        return observations;
    }

    private static async Task AssertNoObservationAsync(
        RuntimeHostObservationSubscription subscription)
    {
        using var cancellationSource =
            new CancellationTokenSource(
                TimeSpan.FromMilliseconds(
                    100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
            {
                await foreach (
                    RuntimeHostObservation observation
                    in subscription.ReadAllAsync(
                        cancellationSource.Token))
                {
                    _ =
                        observation;
                }
            });
    }

    private sealed class StubConnectionDefinition
        : IEndpointConnectionDefinition
    {
        public StubConnectionDefinition(
            EndpointId expectedEndpointId)
        {
            ExpectedEndpointId =
                expectedEndpointId;
        }

        public EndpointConnectionOrigin Origin =>
            EndpointConnectionOrigin.Configured;

        public EndpointId? ExpectedEndpointId
        {
            get;
        }
    }

    private sealed class TestObservedInventory
        : IRuntimeEndpointAttachmentInventory,
          IRuntimeEndpointAttachmentInventoryObservationSource
    {
        private readonly List<RuntimeEndpointAttachmentInventoryEntry>
            _entries;

        private IRuntimeEndpointAttachmentInventoryObserver? _observer;

        public TestObservedInventory(
            params RuntimeEndpointAttachmentInventoryEntry[] entries)
        {
            _entries =
                entries.ToList();
        }

        public bool IsDisposed
        {
            get;
            private set;
        }

        public Task<RuntimeEndpointAttachmentInventoryEntry> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public RuntimeEndpointAttachmentInventoryEntry? Find(
            EndpointId endpointId) =>
            _entries.FirstOrDefault(
                entry =>
                    entry.EndpointId == endpointId);

        public IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> List() =>
            _entries.ToArray();

        public Task<bool> DetachAsync(
            EndpointId endpointId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IDisposable Subscribe(
            IRuntimeEndpointAttachmentInventoryObserver observer)
        {
            _observer =
                observer;

            return new DelegateDisposable(
                () =>
                    _observer =
                        null);
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed =
                true;

            return ValueTask.CompletedTask;
        }

        public void End(
            RuntimeEndpointAttachmentInventoryEntry entry)
        {
            _entries.Remove(
                entry);

            _observer?.OnAttachmentEnded(
                new RuntimeEndpointAttachmentEnded(
                    entry,
                    DateTimeOffset.UtcNow));
        }
    }

    private sealed class DelegateDisposable
        : IDisposable
    {
        private Action? _dispose;

        public DelegateDisposable(
            Action dispose)
        {
            _dispose =
                dispose;
        }

        public void Dispose()
        {
            Interlocked.Exchange(
                    ref _dispose,
                    null)
                ?.Invoke();
        }
    }
}