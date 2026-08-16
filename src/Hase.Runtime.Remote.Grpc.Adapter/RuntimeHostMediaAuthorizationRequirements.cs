using System.Collections.ObjectModel;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Defines the exact permission sets for version 1 media-control operations.
/// Principal and session ownership checks remain additional requirements.
/// </summary>
public static class RuntimeHostMediaAuthorizationRequirements
{
    private static readonly IReadOnlyList<RuntimeHostPermission>
        CapabilityRequirements =
            ReadOnly(
                RuntimeHostPermission.ReadMediaCapabilities);

    private static readonly IReadOnlyList<RuntimeHostPermission>
        VideoStartRequirements =
            ReadOnly(
                RuntimeHostPermission.ReceiveMediaVideo,
                RuntimeHostPermission.StartMediaSession);

    private static readonly IReadOnlyList<RuntimeHostPermission>
        AudioVideoStartRequirements =
            ReadOnly(
                RuntimeHostPermission.ReceiveMediaVideo,
                RuntimeHostPermission.ReceiveMediaAudio,
                RuntimeHostPermission.StartMediaSession);

    private static readonly IReadOnlyList<RuntimeHostPermission>
        NegotiationRequirements =
            ReadOnly(
                RuntimeHostPermission.NegotiateMediaSession);

    private static readonly IReadOnlyList<RuntimeHostPermission>
        StatusRequirements =
            ReadOnly(
                RuntimeHostPermission.ReceiveMediaVideo);

    private static readonly IReadOnlyList<RuntimeHostPermission>
        StopRequirements =
            ReadOnly(
                RuntimeHostPermission.StopMediaSession);

    public static IReadOnlyList<RuntimeHostPermission> ForCapabilities =>
        CapabilityRequirements;

    public static IReadOnlyList<RuntimeHostPermission> ForStart(
        bool includeAudio)
    {
        return includeAudio
            ? AudioVideoStartRequirements
            : VideoStartRequirements;
    }

    public static IReadOnlyList<RuntimeHostPermission> ForNegotiation =>
        NegotiationRequirements;

    public static IReadOnlyList<RuntimeHostPermission> ForStatus =>
        StatusRequirements;

    public static IReadOnlyList<RuntimeHostPermission> ForStop =>
        StopRequirements;

    private static IReadOnlyList<RuntimeHostPermission> ReadOnly(
        params RuntimeHostPermission[] permissions)
    {
        return new ReadOnlyCollection<RuntimeHostPermission>(
            permissions);
    }
}
