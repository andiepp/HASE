using Hase.Client;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Tests;

public sealed class RemoteEventOccurredObservationPayloadTests
{
    [Fact]
    public void Constructor_Values_ShouldPreservePayload()
    {
        var instrumentId =
            new InstrumentId(
                "controller-01");
        var eventPath =
            new DescriptorPath(
                "Controller",
                "ButtonPressed");
        DateTimeOffset occurredAtUtc =
            new(
                2026,
                7,
                27,
                8,
                0,
                0,
                TimeSpan.Zero);
        RemoteValue value =
            RemoteValue.FromBoolean(
                true);

        var payload =
            new RemoteEventOccurredObservationPayload(
                instrumentId,
                eventPath,
                occurredAtUtc,
                value);

        Assert.Equal(
            RemoteObservationKind.EventOccurred,
            payload.Kind);
        Assert.Same(
            instrumentId,
            payload.InstrumentId);
        Assert.Same(
            eventPath,
            payload.EventPath);
        Assert.Equal(
            occurredAtUtc,
            payload.OccurredAtUtc);
        Assert.Same(
            value,
            payload.Value);
    }

    [Fact]
    public void Constructor_WithoutValue_ShouldSucceed()
    {
        var payload =
            new RemoteEventOccurredObservationPayload(
                new InstrumentId(
                    "controller-01"),
                new DescriptorPath(
                    "Controller",
                    "ButtonPressed"),
                DateTimeOffset.UnixEpoch,
                null);

        Assert.Null(
            payload.Value);
    }

    [Fact]
    public void Constructor_NullInstrumentId_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "instrumentId",
            () => new RemoteEventOccurredObservationPayload(
                null!,
                new DescriptorPath(
                    "Controller",
                    "ButtonPressed"),
                DateTimeOffset.UnixEpoch,
                null));
    }

    [Fact]
    public void Constructor_NullEventPath_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "eventPath",
            () => new RemoteEventOccurredObservationPayload(
                new InstrumentId(
                    "controller-01"),
                null!,
                DateTimeOffset.UnixEpoch,
                null));
    }

    [Fact]
    public void Constructor_NonUtcTime_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "occurredAtUtc",
            () => new RemoteEventOccurredObservationPayload(
                new InstrumentId(
                    "controller-01"),
                new DescriptorPath(
                    "Controller",
                    "ButtonPressed"),
                new DateTimeOffset(
                    2026,
                    7,
                    27,
                    10,
                    0,
                    0,
                    TimeSpan.FromHours(
                        2)),
                null));
    }
}
