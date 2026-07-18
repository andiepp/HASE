using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Xunit;

namespace Hase.Runtime.Tests.Runtime;

public sealed class RuntimeEventObserverTests
{
    [Fact]
    public void Subscribe_NullObserver_ShouldThrow()
    {
        // Arrange
        RuntimeEvent runtimeEvent =
            CreateRuntimeEvent();

        // Act
        void Act()
        {
            runtimeEvent.Subscribe(
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "observer",
            exception.ParamName);
    }

    [Fact]
    public void PublishOccurrence_DuplicateSubscription_ShouldNotifyOnce()
    {
        // Arrange
        RuntimeEvent runtimeEvent =
            CreateRuntimeEvent();

        var observer =
            new RecordingObserver();

        runtimeEvent.Subscribe(
            observer);

        runtimeEvent.Subscribe(
            observer);

        var timestampUtc =
            new DateTimeOffset(
                2026,
                7,
                18,
                12,
                30,
                0,
                TimeSpan.Zero);

        // Act
        runtimeEvent.PublishOccurrence(
            timestampUtc,
            null);

        // Assert
        RuntimeEventOccurrence occurrence =
            Assert.Single(
                observer.Occurrences);

        Assert.Same(
            runtimeEvent,
            occurrence.Event);

        Assert.Equal(
            timestampUtc,
            occurrence.TimestampUtc);

        Assert.Null(
            occurrence.Value);
    }

    [Fact]
    public void Unsubscribe_ShouldStopOccurrenceDelivery()
    {
        // Arrange
        RuntimeEvent runtimeEvent =
            CreateRuntimeEvent();

        var observer =
            new RecordingObserver();

        runtimeEvent.Subscribe(
            observer);

        runtimeEvent.Unsubscribe(
            observer);

        // Act
        runtimeEvent.PublishOccurrence(
            DateTimeOffset.UnixEpoch,
            null);

        // Assert
        Assert.Empty(
            observer.Occurrences);
    }

    [Fact]
    public void PublishOccurrence_ObserverThrows_ShouldContinueDelivery()
    {
        // Arrange
        RuntimeEvent runtimeEvent =
            CreateRuntimeEvent();

        var throwingObserver =
            new ThrowingObserver();

        var recordingObserver =
            new RecordingObserver();

        runtimeEvent.Subscribe(
            throwingObserver);

        runtimeEvent.Subscribe(
            recordingObserver);

        var timestampUtc =
            new DateTimeOffset(
                2026,
                7,
                18,
                13,
                0,
                0,
                TimeSpan.Zero);

        // Act
        runtimeEvent.PublishOccurrence(
            timestampUtc,
            "pressed");

        // Assert
        Assert.Equal(
            1,
            throwingObserver.NotificationCount);

        RuntimeEventOccurrence occurrence =
            Assert.Single(
                recordingObserver.Occurrences);

        Assert.Equal(
            timestampUtc,
            occurrence.TimestampUtc);

        Assert.Equal(
            "pressed",
            occurrence.Value);
    }

    private static RuntimeEvent CreateRuntimeEvent()
    {
        var eventDescriptor =
            new EventDescriptor(
                new DescriptorPath(
                    "Controller",
                    "ButtonPressed"),
                "Button Pressed");

        var instrumentDescriptor =
            new InstrumentDescriptor(
                new InstrumentId(
                    "controller-01"),
                "GPIO Controller",
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

        var endpointDescriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "runtime-event-observer-endpoint"),
                [
                    instrumentDescriptor
                ]);

        var context =
            new RuntimeContext();

        RuntimeEndpoint endpoint =
            context.AddEndpoint(
                endpointDescriptor);

        RuntimeInstrument instrument =
            Assert.Single(
                endpoint.Instruments);

        return Assert.Single(
            instrument.Events);
    }

    private sealed class RecordingObserver
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

    private sealed class ThrowingObserver
        : IRuntimeEventObserver
    {
        public int NotificationCount
        {
            get;
            private set;
        }

        public void OnRuntimeEventOccurred(
            RuntimeEventOccurrence occurrence)
        {
            ArgumentNullException.ThrowIfNull(
                occurrence);

            NotificationCount++;

            throw new InvalidOperationException(
                "Expected runtime event observer failure.");
        }
    }
}