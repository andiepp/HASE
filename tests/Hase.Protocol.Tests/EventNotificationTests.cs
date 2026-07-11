using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class EventNotificationTests
{
    [Fact]
    public void Constructor_SetsProtocolProperties()
    {
        EventNotification notification = new(
            new InstrumentId("Instrument-1"),
            DescriptorPath.Parse("DDS.PLL.LockLost"),
            new DateTimeOffset(
                2026,
                7,
                11,
                12,
                0,
                0,
                TimeSpan.Zero),
            null);

        Assert.Equal(
            ProtocolVersion.Current,
            notification.Version);

        Assert.Equal(
            ProtocolMessageRole.Notification,
            notification.Role);

        Assert.Equal(
            ProtocolMessageType.EventNotification,
            notification.MessageType);
    }

    [Fact]
    public void Constructor_StoresEventInformation()
    {
        InstrumentId instrumentId = new("Instrument-1");

        DescriptorPath eventPath =
            DescriptorPath.Parse("DDS.Measurement.Completed");

        DateTimeOffset timestamp =
            new(
                2026,
                7,
                11,
                12,
                0,
                0,
                TimeSpan.Zero);

        EventNotification notification = new(
            instrumentId,
            eventPath,
            timestamp,
            "Run42.csv");

        Assert.Equal(
            instrumentId,
            notification.InstrumentId);

        Assert.Equal(
            eventPath,
            notification.EventPath);

        Assert.Equal(
            timestamp,
            notification.TimestampUtc);

        Assert.Equal(
            "Run42.csv",
            notification.Value);
    }

    [Fact]
    public void Constructor_AllowsNullPayload()
    {
        EventNotification notification = new(
            new InstrumentId("Instrument-1"),
            DescriptorPath.Parse("DDS.Reset"),
            DateTimeOffset.UtcNow,
            null);

        Assert.Null(
            notification.Value);
    }

    [Fact]
    public void EqualNotifications_AreEqual()
    {
        DateTimeOffset timestamp =
            new(
                2026,
                7,
                11,
                12,
                0,
                0,
                TimeSpan.Zero);

        EventNotification first = new(
            new InstrumentId("Instrument-1"),
            DescriptorPath.Parse("DDS.Reset"),
            timestamp,
            null);

        EventNotification second = new(
            new InstrumentId("Instrument-1"),
            DescriptorPath.Parse("DDS.Reset"),
            timestamp,
            null);

        Assert.Equal(first, second);
    }
}