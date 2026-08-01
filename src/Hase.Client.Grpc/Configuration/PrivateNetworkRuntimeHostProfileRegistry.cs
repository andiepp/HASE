using Hase.Client.Configuration;

namespace Hase.Client.Grpc.Configuration;

/// <summary>
/// Holds immutable ordered private-network deployment references for one
/// client runtime-host profile registry.
/// </summary>
public sealed class PrivateNetworkRuntimeHostProfileRegistry
{
    private readonly IReadOnlyDictionary<
        RuntimeHostProfileId,
        PrivateNetworkRuntimeHostProfile> profilesById;

    public PrivateNetworkRuntimeHostProfileRegistry(
        IEnumerable<PrivateNetworkRuntimeHostProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(
            profiles);

        PrivateNetworkRuntimeHostProfile[] snapshot =
            profiles.ToArray();

        if (snapshot.Any(
                profile =>
                    profile is null))
        {
            throw new ArgumentException(
                "A private-network runtime-host profile registry must not contain null.",
                nameof(profiles));
        }

        CoreProfiles =
            new RuntimeHostProfileRegistry(
                snapshot.Select(
                    profile =>
                        profile.Profile));

        Profiles =
            Array.AsReadOnly(
                snapshot);
        profilesById =
            new Dictionary<
                RuntimeHostProfileId,
                PrivateNetworkRuntimeHostProfile>(
                snapshot.ToDictionary(
                    profile =>
                        profile.Profile.ProfileId));
    }

    public RuntimeHostProfileRegistry CoreProfiles
    {
        get;
    }

    public IReadOnlyList<PrivateNetworkRuntimeHostProfile> Profiles
    {
        get;
    }

    public bool TryGet(
        RuntimeHostProfileId profileId,
        out PrivateNetworkRuntimeHostProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(
            profileId);

        return profilesById.TryGetValue(
            profileId,
            out profile);
    }
}
