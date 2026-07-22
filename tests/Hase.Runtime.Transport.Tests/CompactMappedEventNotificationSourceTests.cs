using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactMappedEventNotificationSourceTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly DescriptorPath EventPath =
        DescriptorPath.Parse(
            "Controller.ButtonPressed");

    [Fact]
    public void Activate_CurrentConnectionEvent_ShouldPublishMappedNotification()
    {
        var source =
            CreateSource();

        TestCompactProtocolConnection protocolConnection =
            new();

        CompactEndpointConnection endpointConnection =
            CreateEndpointConnection(
                protocolConnection);

        var received =
            new List<CompactMappedEventNotification>();

        source.MappedEventNotificationReceived +=
            received.Add;

        source.Activate(
            endpointConnection);

        protocolConnection.Publish(
            new CompactEventNotification(
                eventId: 0x01,
                ReadOnlyMemory<byte>.Empty));

        CompactMappedEventNotification notification =
            Assert.Single(
                received);

        Assert.Equal(
            InstrumentId,
            notification.Mapping.InstrumentId);

        Assert.Equal(
            EventPath,
            notification.Mapping.EventPath);
    }

    [Fact]
    public void Activate_Replacement_ShouldSuppressPreviousConnection()
    {
        var source =
            CreateSource();

        TestCompactProtocolConnection firstProtocolConnection =
            new();

        TestCompactProtocolConnection secondProtocolConnection =
            new();

        CompactEndpointConnection firstConnection =
            CreateEndpointConnection(
                firstProtocolConnection);

        CompactEndpointConnection secondConnection =
            CreateEndpointConnection(
                secondProtocolConnection);

        var received =
            new List<CompactMappedEventNotification>();

        source.MappedEventNotificationReceived +=
            received.Add;

        source.Activate(
            firstConnection);

        source.Activate(
            secondConnection);

        firstProtocolConnection.Publish(
            new CompactEventNotification(
                eventId: 0x01,
                ReadOnlyMemory<byte>.Empty));

        Assert.Empty(
            received);

        secondProtocolConnection.Publish(
            new CompactEventNotification(
                eventId: 0x01,
                ReadOnlyMemory<byte>.Empty));

        Assert.Single(
            received);

        Assert.Same(
            secondConnection,
            source.ActiveConnection);
    }

    [Fact]
    public void Deactivate_PreviousConnectionEvent_ShouldBeIgnored()
    {
        var source =
            CreateSource();

        TestCompactProtocolConnection protocolConnection =
            new();

        CompactEndpointConnection connection =
            CreateEndpointConnection(
                protocolConnection);

        var received =
            new List<CompactMappedEventNotification>();

        source.MappedEventNotificationReceived +=
            received.Add;

        source.Activate(
            connection);

        source.Deactivate();

        protocolConnection.Publish(
            new CompactEventNotification(
                eventId: 0x01,
                ReadOnlyMemory<byte>.Empty));

        Assert.Empty(
            received);

        Assert.Null(
            source.ActiveConnection);
    }

    [Fact]
    public void Activate_SameConnectionTwice_ShouldDeliverOnce()
    {
        var source =
            CreateSource();

        TestCompactProtocolConnection protocolConnection =
            new();

        CompactEndpointConnection connection =
            CreateEndpointConnection(
                protocolConnection);

        int count =
            0;

        source.MappedEventNotificationReceived +=
            notification =>
            {
                count++;
            };

        source.Activate(
            connection);

        source.Activate(
            connection);

        protocolConnection.Publish(
            new CompactEventNotification(
                eventId: 0x01,
                ReadOnlyMemory<byte>.Empty));

        Assert.Equal(
            1,
            count);
    }

    [Fact]
    public void Activate_UnknownEvent_ShouldRejectThroughResolver()
    {
        var source =
            CreateSource();

        TestCompactProtocolConnection protocolConnection =
            new();

        source.Activate(
            CreateEndpointConnection(
                protocolConnection));

        void Act()
        {
            protocolConnection.Publish(
                new CompactEventNotification(
                    eventId: 0x02,
                    ReadOnlyMemory<byte>.Empty));
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Constructor_NullResolver_ShouldThrow()
    {
        void Act()
        {
            _ =
                new CompactMappedEventNotificationSource(
                    null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Activate_NullConnection_ShouldThrow()
    {
        var source =
            CreateSource();

        void Act()
        {
            source.Activate(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static CompactMappedEventNotificationSource
        CreateSource()
    {
        var eventDescriptor =
            new EventDescriptor(
                EventPath,
                "Button Pressed");

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Controller",
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

        var definition =
            new EndpointDescriptorDefinition(
                instruments:
                [
                    instrument
                ],
                metadata:
                    new());

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