using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Runtime;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolRuntimeEndpointEventRouterTests
{
    private static readonly InstrumentId ControllerInstrumentId =
        new(
            "controller-01");

    private static readonly DescriptorPath ButtonPressedPath =
        new(
            "Controller",
            "ButtonPressed");

    [Fact]
    public void Constructor_NullRuntimeEndpoint_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new ProtocolRuntimeEndpointEventRouter(
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "runtimeEndpoint",
            exception.ParamName);
    }

    [Fact]
    public void OnProtocolNotification_NullNotification_ShouldThrow()
    {
        // Arrange
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var router =
            new ProtocolRuntimeEndpointEventRouter(
                runtimeEndpoint);

        // Act
        void Act()
        {
            router.OnProtocolNotification(
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "notification",
            exception.ParamName);
    }

    [Fact]
    public void OnProtocolNotification_MatchingEvent_ShouldPublishOccurrence()
    {
        // Arrange
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        RuntimeEvent runtimeEvent =
            GetRuntimeEvent(
                runtimeEndpoint);

        var observer =
            new RecordingEventObserver();

        runtimeEvent.Subscribe(
            observer);

        var router =
            new ProtocolRuntimeEndpointEventRouter(
                runtimeEndpoint);

        var timestampUtc =
            new DateTimeOffset(
                2026,
                7,
                18,
                15,
                0,
                0,
                TimeSpan.Zero);

        object value =
            "pressed";

        var notification =
            new EventNotification(
                ControllerInstrumentId,
                ButtonPressedPath,
                timestampUtc,
                value);

        // Act
        router.OnProtocolNotification(
            notification);

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

        Assert.Same(
            value,
            occurrence.Value);
    }

    [Fact]
    public void OnProtocolNotification_UnknownInstrument_ShouldIgnore()
    {
        // Arrange
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        RuntimeEvent runtimeEvent =
            GetRuntimeEvent(
                runtimeEndpoint);

        var observer =
            new RecordingEventObserver();

        runtimeEvent.Subscribe(
            observer);

        var router =
            new ProtocolRuntimeEndpointEventRouter(
                runtimeEndpoint);

        var notification =
            new EventNotification(
                new InstrumentId(
                    "unknown-controller"),
                ButtonPressedPath,
                DateTimeOffset.UnixEpoch,
                null);

        // Act
        router.OnProtocolNotification(
            notification);

        // Assert
        Assert.Empty(
            observer.Occurrences);
    }

    [Fact]
    public void OnProtocolNotification_UnknownEventPath_ShouldIgnore()
    {
        // Arrange
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        RuntimeEvent runtimeEvent =
            GetRuntimeEvent(
                runtimeEndpoint);

        var observer =
            new RecordingEventObserver();

        runtimeEvent.Subscribe(
            observer);

        var router =
            new ProtocolRuntimeEndpointEventRouter(
                runtimeEndpoint);

        var notification =
            new EventNotification(
                ControllerInstrumentId,
                new DescriptorPath(
                    "Controller",
                    "UnknownEvent"),
                DateTimeOffset.UnixEpoch,
                null);

        // Act
        router.OnProtocolNotification(
            notification);

        // Assert
        Assert.Empty(
            observer.Occurrences);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var eventDescriptor =
            new EventDescriptor(
                ButtonPressedPath,
                "Button Pressed")
            {
                Description =
                    "Raised when the physical pushbutton is pressed."
            };

        var instrumentDescriptor =
            new InstrumentDescriptor(
                ControllerInstrumentId,
                "ESP32 GPIO Controller",
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
                    "event-router-endpoint"),
                [
                    instrumentDescriptor
                ]);

        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            endpointDescriptor);
    }

    private static RuntimeEvent GetRuntimeEvent(
        RuntimeEndpoint runtimeEndpoint)
    {
        RuntimeInstrument runtimeInstrument =
            runtimeEndpoint.FindInstrument(
                ControllerInstrumentId)
            ?? throw new InvalidOperationException(
                "The controller runtime instrument was not found.");

        return runtimeInstrument.FindEvent(
            ButtonPressedPath)
            ?? throw new InvalidOperationException(
                "The button-pressed runtime event was not found.");
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
}