using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactEndpointHealthProbeOptionsTests
{
    [Fact]
    public void Default_ShouldUseApprovedTiming()
    {
        CompactEndpointHealthProbeOptions options =
            CompactEndpointHealthProbeOptions.Default;

        Assert.Equal(
            TimeSpan.FromSeconds(
                1),
            options.ProbeInterval);

        Assert.Equal(
            TimeSpan.FromSeconds(
                3),
            options.ProbeTimeout);
    }

    [Fact]
    public void Constructor_CustomValues_ShouldRetainValues()
    {
        var options =
            new CompactEndpointHealthProbeOptions(
                probeInterval:
                    TimeSpan.FromMilliseconds(
                        250),
                probeTimeout:
                    TimeSpan.FromSeconds(
                        2));

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                250),
            options.ProbeInterval);

        Assert.Equal(
            TimeSpan.FromSeconds(
                2),
            options.ProbeTimeout);
    }

    [Theory]
    [InlineData(
        0)]
    [InlineData(
        -1)]
    public void Constructor_NonPositiveInterval_ShouldThrow(
        int milliseconds)
    {
        void Act()
        {
            _ = new CompactEndpointHealthProbeOptions(
                probeInterval:
                    TimeSpan.FromMilliseconds(
                        milliseconds),
                probeTimeout:
                    TimeSpan.FromSeconds(
                        3));
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Theory]
    [InlineData(
        0)]
    [InlineData(
        -1)]
    public void Constructor_NonPositiveTimeout_ShouldThrow(
        int milliseconds)
    {
        void Act()
        {
            _ = new CompactEndpointHealthProbeOptions(
                probeInterval:
                    TimeSpan.FromSeconds(
                        1),
                probeTimeout:
                    TimeSpan.FromMilliseconds(
                        milliseconds));
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Constructor_TimeoutLongerThanInterval_ShouldSucceed()
    {
        var options =
            new CompactEndpointHealthProbeOptions(
                probeInterval:
                    TimeSpan.FromSeconds(
                        1),
                probeTimeout:
                    TimeSpan.FromSeconds(
                        10));

        Assert.True(
            options.ProbeTimeout
            > options.ProbeInterval);
    }
}