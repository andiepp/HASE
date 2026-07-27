using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemotePropertyValueTests
{
    [Fact]
    public void Constructor_Values_ShouldPreservePropertyValue()
    {
        RemoteValue value =
            RemoteValue.FromNumeric(
                23.5);
        DateTimeOffset timestampUtc =
            new(
                2026,
                7,
                27,
                8,
                0,
                0,
                TimeSpan.Zero);

        var result =
            new RemotePropertyValue(
                value,
                timestampUtc,
                RemotePropertyQuality.Good);

        Assert.Same(
            value,
            result.Value);
        Assert.Equal(
            timestampUtc,
            result.TimestampUtc);
        Assert.Equal(
            RemotePropertyQuality.Good,
            result.Quality);
    }

    [Fact]
    public void Constructor_MissingValue_ShouldSucceed()
    {
        var result =
            new RemotePropertyValue(
                null,
                DateTimeOffset.UnixEpoch,
                RemotePropertyQuality.Bad);

        Assert.Null(
            result.Value);
    }

    [Fact]
    public void Constructor_NonUtcTimestamp_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "timestampUtc",
            () => new RemotePropertyValue(
                RemoteValue.FromBoolean(
                    true),
                new DateTimeOffset(
                    2026,
                    7,
                    27,
                    10,
                    0,
                    0,
                    TimeSpan.FromHours(
                        2)),
                RemotePropertyQuality.Good));
    }

    [Fact]
    public void Constructor_UnspecifiedQuality_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "quality",
            () => new RemotePropertyValue(
                RemoteValue.FromBoolean(
                    true),
                DateTimeOffset.UnixEpoch,
                RemotePropertyQuality.Unspecified));
    }

    [Fact]
    public void Constructor_UndefinedQuality_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "quality",
            () => new RemotePropertyValue(
                RemoteValue.FromBoolean(
                    true),
                DateTimeOffset.UnixEpoch,
                (RemotePropertyQuality) 99));
    }
}
