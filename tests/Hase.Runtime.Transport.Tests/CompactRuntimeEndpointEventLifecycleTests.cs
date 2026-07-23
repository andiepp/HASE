using Hase.CompactProtocol;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactRuntimeEndpointEventLifecycleTests
{
    private static readonly EndpointId EndpointId =
        new(
            "arduino-uno-01");

    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly PropertyId PropertyId =
        new(
            "led-state");

    private static readonly DescriptorPath EventPath =
        DescriptorPath.Parse(
            "Controller.ButtonPressed");

    [Fact]
    public async Task ConnectAsync_EventDuringSynchronization_ShouldNotDeliverOrReplay()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        RuntimeEvent runtimeEvent =
            GetRuntimeEvent(
                runtimeEndpoint);

        var observer =
            new RecordingEventObserver();

        runtimeEvent.Subscribe(
            observer);

        var protocolConnection =
            new TestCompactSerialProtocolConnection(
                publishEventDuringFirstExchange: true);

        var eventSource =
            CreateEventSource(
                definition);

        _ =
            new CompactRuntimeEndpointEventRouter(
                eventSource,
                runtimeEndpoint,
                TimeProvider.System);

        await using var coordinator =
            CreateCoordinator(
                new QueueCompactEndpointConnectionFactory(
                    CreateConnection(
                        definition,
                        protocolConnection)),
                definition,
                runtimeEndpoint,
                eventSource);

        await coordinator.ConnectAsync();

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Empty(
            observer.Occurrences);

        protocolConnection.PublishEvent();

        Assert.Single(
            observer.Occurrences);
    }

    [Fact]
    public async Task Recovery_ShouldKeepObserverSuppressOfflineAndStaleEventsAndNotReplay()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        RuntimeEvent runtimeEvent =
            GetRuntimeEvent(
                runtimeEndpoint);

        var observer =
            new RecordingEventObserver();

        runtimeEvent.Subscribe(
            observer);

        var firstProtocolConnection =
            new TestCompactSerialProtocolConnection();

        var secondProtocolConnection =
            new TestCompactSerialProtocolConnection();

        var eventSource =
            CreateEventSource(
                definition);

        _ =
            new CompactRuntimeEndpointEventRouter(
                eventSource,
                runtimeEndpoint,
                TimeProvider.System);

        await using var coordinator =
            CreateCoordinator(
                new QueueCompactEndpointConnectionFactory(
                    CreateConnection(
                        definition,
                        firstProtocolConnection),
                    CreateConnection(
                        definition,
                        secondProtocolConnection)),
                definition,
                runtimeEndpoint,
                eventSource);

        await coordinator.ConnectAsync();

        firstProtocolConnection.PublishEvent();

        Assert.Single(
            observer.Occurrences);

        coordinator.MarkFaulted(
            "Simulated compact connection loss.");

        firstProtocolConnection.PublishEvent();

        Assert.Single(
            observer.Occurrences);

        await coordinator.DetachFaultedConnectionAsync();

        firstProtocolConnection.PublishEvent();

        Assert.Single(
            observer.Occurrences);

        await coordinator.ReconnectAsync();

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Single(
            observer.Occurrences);

        secondProtocolConnection.PublishEvent();

        Assert.Equal(
            2,
            observer.Occurrences.Count);

        Assert.All(
            observer.Occurrences,
            occurrence =>
                Assert.Same(
                    runtimeEvent,
                    occurrence.Event));

        firstProtocolConnection.PublishEvent();

        Assert.Equal(
            2,
            observer.Occurrences.Count);

        Assert.Equal(
            1,
            firstProtocolConnection.DisposeCallCount);
    }

    [Fact]
    public async Task DisposeAsync_ShouldStopEventDeliveryBeforeConnectionDisposal()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        RuntimeEvent runtimeEvent =
            GetRuntimeEvent(
                runtimeEndpoint);

        var observer =
            new RecordingEventObserver();

        runtimeEvent.Subscribe(
            observer);

        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        var eventSource =
            CreateEventSource(
                definition);

        _ =
            new CompactRuntimeEndpointEventRouter(
                eventSource,
                runtimeEndpoint,
                TimeProvider.System);

        var coordinator =
            CreateCoordinator(
                new QueueCompactEndpointConnectionFactory(
                    CreateConnection(
                        definition,
                        protocolConnection)),
                definition,
                runtimeEndpoint,
                eventSource);

        await coordinator.ConnectAsync();

        protocolConnection.PublishEvent();

        Assert.Single(
            observer.Occurrences);

        await coordinator.DisposeAsync();

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);

        protocolConnection.PublishEvent();

        Assert.Single(
            observer.Occurrences);

        await coordinator.DisposeAsync();

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
    }

    private static CompactRuntimeEndpointConnectionCoordinator
        CreateCoordinator(
            ICompactEndpointConnectionFactory connectionFactory,
            EndpointDescriptorDefinition definition,
            RuntimeEndpoint runtimeEndpoint,
            CompactMappedEventNotificationSource eventSource)
    {
        return new CompactRuntimeEndpointConnectionCoordinator(
            connectionFactory,
            new SerialTransportOptions(
                "COM10",
                115200),
            CreatePropertyMap(
                definition),
            runtimeEndpoint,
            new EndpointDescriptorCompatibilityValidator(),
            new CompactEndpointConnectionOwner(),
            eventSource,
            TimeProvider.System);
    }

    private static CompactMappedEventNotificationSource
        CreateEventSource(
            EndpointDescriptorDefinition definition)
    {
        var eventMap =
            new CompactEventMap(
                definition,
                [
                    new CompactEventMapping(
                        compactEventId: 0x01,
                        InstrumentId,
                        EventPath,
                        CompactEventValueEncoding.None)
                ]);

        return new CompactMappedEventNotificationSource(
            new CompactEventNotificationResolver(
                eventMap));
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint(
        EndpointDescriptorDefinition definition)
    {
        return new RuntimeContext()
            .AddEndpoint(
                definition.Materialize(
                    EndpointId));
    }

    private static RuntimeEvent GetRuntimeEvent(
        RuntimeEndpoint runtimeEndpoint)
    {
        RuntimeInstrument runtimeInstrument =
            runtimeEndpoint.FindInstrument(
                InstrumentId)
            ?? throw new InvalidOperationException(
                "The controller runtime instrument was not found.");

        return runtimeInstrument.FindEvent(
            EventPath)
            ?? throw new InvalidOperationException(
                "The button-pressed runtime event was not found.");
    }

    private static CompactEndpointConnection CreateConnection(
        EndpointDescriptorDefinition definition,
        ICompactSerialProtocolConnection protocolConnection)
    {
        return new CompactEndpointConnection(
            definition.Materialize(
                EndpointId),
            protocolConnection);
    }

    private static EndpointDescriptorDefinition CreateDefinition()
    {
        var property =
            new PropertyDescriptor(
                PropertyId,
                new DescriptorPath(
                    "Led",
                    "State"),
                "Built-in LED State",
                new BooleanDataDescriptor())
            {
                AccessMode =
                    PropertyAccessMode.Read
            };

        var eventDescriptor =
            new EventDescriptor(
                EventPath,
                "Button Pressed");

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Arduino Uno GPIO Controller",
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
                            eventDescriptor
                        ])
            };

        return new EndpointDescriptorDefinition(
            metadata:
                new EndpointMetadata
                {
                    DisplayName =
                        "Arduino Uno Compact Endpoint"
                },
            instruments:
            [
                instrument
            ]);
    }

    private static CompactPropertyMap CreatePropertyMap(
        EndpointDescriptorDefinition definition)
    {
        return new CompactPropertyMap(
            definition,
            mappings:
            [
                new CompactPropertyMapping(
                    compactPropertyId: 0x01,
                    InstrumentId,
                    PropertyId,
                    CompactPropertyValueEncoding.Boolean)
            ]);
    }

    private sealed class QueueCompactEndpointConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        private readonly Queue<CompactEndpointConnection> _connections;

        public QueueCompactEndpointConnectionFactory(
            params CompactEndpointConnection[] connections)
        {
            _connections =
                new Queue<CompactEndpointConnection>(
                    connections);
        }

        public int ConnectCallCount
        {
            get;
            private set;
        }

        public Task<CompactEndpointConnection> ConnectAsync(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ConnectCallCount++;

            if (_connections.Count == 0)
            {
                throw new InvalidOperationException(
                    "No compact endpoint connection remains.");
            }

            return Task.FromResult(
                _connections.Dequeue());
        }
    }

    private sealed class RecordingEventObserver
        : IRuntimeEventObserver
    {
        private readonly List<RuntimeEventOccurrence>
            _occurrences =
                [];

        public IReadOnlyList<RuntimeEventOccurrence>
            Occurrences =>
                _occurrences;

        public void OnRuntimeEventOccurred(
            RuntimeEventOccurrence occurrence)
        {
            ArgumentNullException.ThrowIfNull(
                occurrence);

            _occurrences.Add(
                occurrence);
        }
    }

    private sealed class TestCompactSerialProtocolConnection
        : ICompactSerialProtocolConnection
    {
        private readonly bool _publishEventDuringFirstExchange;
        private bool _firstExchange =
            true;

        public TestCompactSerialProtocolConnection(
            bool publishEventDuringFirstExchange = false)
        {
            _publishEventDuringFirstExchange =
                publishEventDuringFirstExchange;
        }

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public event Action<CompactEventNotification>?
            EventNotificationReceived;

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

        public int ExchangeCallCount
        {
            get;
            private set;
        }

        public int DisposeCallCount
        {
            get;
            private set;
        }

        public void PublishEvent()
        {
            EventNotificationReceived?.Invoke(
                new CompactEventNotification(
                    eventId: 0x01,
                    ReadOnlyMemory<byte>.Empty));
        }

        public Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ExchangeCallCount++;

            if (_firstExchange)
            {
                _firstExchange =
                    false;

                if (_publishEventDuringFirstExchange)
                {
                    PublishEvent();
                }
            }

            CompactReadPropertyRequest readRequest =
                CompactReadPropertyCodec.DecodeRequest(
                    request);

            return Task.FromResult(
                CompactReadPropertyCodec.EncodeResponse(
                    new CompactReadPropertyResponse(
                        readRequest.CorrelationId,
                        readRequest.PropertyId,
                        CompactPropertyReadStatus.Success,
                        value:
                        new byte[]
                        {
                            0x01
                        })));
        }

        public void Invalidate()
        {
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            return ValueTask.CompletedTask;
        }
    }
}