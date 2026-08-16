using Hase.Runtime.Media;
using Hase.Runtime.Remote.Grpc.Adapter;
using MediaV1 = Hase.Runtime.Media.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostMediaCapabilityMapperTests
{
    [Fact]
    public void MapPublishesOrderedSanitizedLogicalSourcesOnly()
    {
        IReadOnlyList<RuntimeHostMediaSourceConfiguration> sources =
        [
            new(
                new("usb-camera", "generation-02"),
                "windows-device-secret-02",
                null,
                RuntimeHostMediaSourceAvailability.Busy,
                "USB camera"),
            new(
                new("built-in-camera", "generation-01"),
                "windows-device-secret-01",
                "windows-microphone-secret",
                RuntimeHostMediaSourceAvailability.Idle,
                "Built-in camera")
        ];

        var result = new RuntimeHostMediaCapabilityMapper().Map(sources);

        Assert.Equal(["Built-in camera", "USB camera"],
            result.Select(item => item.DisplayName));
        Assert.Equal("built-in-camera", result[0].Target.MediaSourceId);
        Assert.Equal("generation-01",
            result[0].Target.MediaSourceGeneration);
        Assert.Equal(MediaV1.MediaSourceAvailability.Idle,
            result[0].Availability);
        Assert.True(result[0].SupportsAudio);
        Assert.False(result[1].SupportsAudio);
        Assert.DoesNotContain(
            result,
            item => item.ToString().Contains(
                "windows-device-secret",
                StringComparison.Ordinal));
    }
}
