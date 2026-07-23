using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactRuntimeEndpointEventRouterTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly DescriptorPath EventPath =
        DescriptorPath.Parse(
            "Controller.ButtonPressed");

    private static readonly DateTimeOffset ObservationTimeUtc =
        new(
            2026,
            7,
            22,
            20,
            30,
            0,
            TimeSpan.Zero);

    [Fact]
    public void MappedNotification_ShouldPublishNativeRuntimeOccurrence()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        RuntimeEvent runtimeEvent =
            GetRuntimeEvent(
                runtimeEndpoint);

        var observer =
            new RecordingEventObserver();

        runtimeEvent.Subscribe(
            observer);

        CompactMappedEventNotificationSource source =
            CreateSource();

        _ =
            new CompactRuntimeEndpointEventRouter(
                source,
                runtimeEndpoint,
                new FixedTimeProvider(
                    ObservationTimeUtc));

        var protocolConnection =
            new TestCompactProtocolConnection();

        source.Activate(
            CreateEndpointConnection(
                protocolConnection));

        protocolConnection.Publish(
            new CompactEventNotification(
                eventId: 0x01,
                ReadOnlyMemory<byte>.Empty));

        RuntimeEventOccurrence occurrence =
            Assert.Single(
                observer.Occurrences);

        Assert.Same(
            runtimeEvent,
            occurrence.Event);

        Assert.Equal(
            ObservationTimeUtc,
            occurrence.TimestampUtc);

        Assert.Null(
            occurrence.Value);
    }

    [Fact]
    public void Constructor_NullEventSource_ShouldThrow()
    {
        void Act()
        {
            _ =
                new CompactRuntimeEndpointEventRouter(
                    null!,
                    CreateRuntimeEndpoint(),
                    TimeProvider.System);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullRuntimeEndpoint_ShouldThrow()
    {
        void Act()
        {
            _ =
                new CompactRuntimeEndpointEventRouter(
                    CreateSource(),
                    null!,
                    TimeProvider.System);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullTimeProvider_ShouldThrow()
    {
        void Act()
        {
            _ =
                new CompactRuntimeEndpointEventRouter(
                    CreateSource(),
                    CreateRuntimeEndpoint(),
                    null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static CompactMappedEventNotificationSource
        CreateSource()
    {
        EndpointDescriptorDefinition definition =
            CreateDescriptorDefinition();

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

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        return new RuntimeContext()
            .CreateEndpoint(
                CreateDescriptorDefinition()
                    .Materialize(
                        new EndpointId(
                            "arduino-uno-01")));
    }

    private static EndpointDescriptorDefinition
        CreateDescriptorDefinition()
    {
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
                        events:
                        [
                            eventDescriptor
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

    private static CompactEndpointConnection
        CreateEndpointConnection(
            ICompactSerialProtocolConnection connection)
    {
        EndpointDescriptor descriptor =
            new EndpointDescriptorDefinition()
                .Materialize(
                    new EndpointId(
                        "arduino-uno-01"));

        return new CompactEndpointConnection(
            descriptor,
            connection);
    }

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(
            DateTimeOffset utcNow)
        {
            _utcNow =
                utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
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
            _occurrences.Add(
                occurrence);
        }
    }

    private sealed class TestCompactProtocolConnection
        : ICompactSerialProtocolConnection
    {
        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public event Action<CompactEventNotification>?
            EventNotificationReceived;

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

        public void Publish(
            CompactEventNotification notification)
        {
            EventNotificationReceived?.Invoke(
                notification);
        }

        public Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void Invalidate()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}