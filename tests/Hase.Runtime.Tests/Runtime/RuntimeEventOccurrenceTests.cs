using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Xunit;

namespace Hase.Runtime.Tests.Runtime;

public sealed class RuntimeEventOccurrenceTests
{
    [Fact]
    public void Constructor_NullRuntimeEvent_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new RuntimeEventOccurrence(
                null!,
                DateTimeOffset.UnixEpoch,
                null);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "runtimeEvent",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_NonUtcTimestamp_ShouldThrow()
    {
        // Arrange
        RuntimeEvent runtimeEvent =
            CreateRuntimeEvent();

        var timestamp =
            new DateTimeOffset(
                2026,
                7,
                18,
                14,
                0,
                0,
                TimeSpan.FromHours(
                    2));

        // Act
        void Act()
        {
            _ = new RuntimeEventOccurrence(
                runtimeEvent,
                timestamp,
                null);
        }

        // Assert
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                Act);

        Assert.Equal(
            "timestampUtc",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_ValidValues_ShouldRetainOccurrenceData()
    {
        // Arrange
        RuntimeEvent runtimeEvent =
            CreateRuntimeEvent();

        var timestampUtc =
            new DateTimeOffset(
                2026,
                7,
                18,
                12,
                0,
                0,
                TimeSpan.Zero);

        object value =
            "button-pressed";

        // Act
        var occurrence =
            new RuntimeEventOccurrence(
                runtimeEvent,
                timestampUtc,
                value);

        // Assert
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
                    "runtime-event-endpoint"),
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
}