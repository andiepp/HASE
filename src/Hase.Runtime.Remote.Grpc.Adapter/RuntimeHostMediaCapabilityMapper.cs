using Hase.Runtime.Media;
using MediaV1 = global::Hase.Runtime.Media.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Projects only operator-defined logical source data. Local Windows device
/// identities and operating-system friendly names never cross this boundary.
/// </summary>
public sealed class RuntimeHostMediaCapabilityMapper
{
    public IReadOnlyList<MediaV1.MediaSourceCapability> Map(
        IReadOnlyList<RuntimeHostMediaSourceConfiguration> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return sources
            .OrderBy(source => source.DisplayName, StringComparer.Ordinal)
            .ThenBy(source => source.Target.MediaSourceId, StringComparer.Ordinal)
            .Select(Map)
            .ToArray();
    }

    private static MediaV1.MediaSourceCapability Map(
        RuntimeHostMediaSourceConfiguration source)
    {
        var capability = new MediaV1.MediaSourceCapability
        {
            Target = new MediaV1.MediaSourceTarget
            {
                MediaSourceId = source.Target.MediaSourceId,
                MediaSourceGeneration = source.Target.MediaSourceGeneration
            },
            DisplayName = source.DisplayName,
            Availability = source.Availability switch
            {
                RuntimeHostMediaSourceAvailability.Unavailable =>
                    MediaV1.MediaSourceAvailability.Unavailable,
                RuntimeHostMediaSourceAvailability.Idle =>
                    MediaV1.MediaSourceAvailability.Idle,
                RuntimeHostMediaSourceAvailability.Busy =>
                    MediaV1.MediaSourceAvailability.Busy,
                RuntimeHostMediaSourceAvailability.Faulted =>
                    MediaV1.MediaSourceAvailability.Faulted,
                _ => MediaV1.MediaSourceAvailability.Unspecified
            },
            SupportsVideo = true,
            SupportsAudio = source.SupportsAudio
        };
        capability.VideoProfiles.Add(
            new MediaV1.MediaVideoProfile
            {
                Codec = MediaV1.MediaVideoCodec.Vp8,
                Width = 640,
                Height = 480,
                MaximumFramesPerSecond = 30
            });
        if (source.SupportsAudio)
        {
            capability.AudioProfiles.Add(
                new MediaV1.MediaAudioProfile
                {
                    Codec = MediaV1.MediaAudioCodec.Opus,
                    SampleRateHertz = 48_000,
                    MaximumChannelCount = 2
                });
        }

        return capability;
    }
}
