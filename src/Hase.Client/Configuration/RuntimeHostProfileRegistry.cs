namespace Hase.Client.Configuration;

/// <summary>
/// Holds one immutable ordered collection of configured runtime-host profiles.
/// </summary>
public sealed class RuntimeHostProfileRegistry
{
    public const int MaximumProfileCount =
        64;

    private readonly IReadOnlyDictionary<
        RuntimeHostProfileId,
        RuntimeHostProfile> profilesById;

    public RuntimeHostProfileRegistry(
        IEnumerable<RuntimeHostProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(
            profiles);

        RuntimeHostProfile[] snapshot =
            profiles.ToArray();

        if (snapshot.Length > MaximumProfileCount)
        {
            throw new ArgumentException(
                $"A runtime-host profile registry must not contain more than {MaximumProfileCount} profiles.",
                nameof(profiles));
        }

        if (snapshot.Any(
                profile =>
                    profile is null))
        {
            throw new ArgumentException(
                "A runtime-host profile registry must not contain null.",
                nameof(profiles));
        }

        if (snapshot
            .GroupBy(
                profile =>
                    profile.ProfileId)
            .Any(
                group =>
                    group.Count() > 1))
        {
            throw new ArgumentException(
                "A runtime-host profile registry must not contain duplicate profile identities.",
                nameof(profiles));
        }

        if (snapshot
            .Where(
                profile =>
                    profile.IsEnabled)
            .GroupBy(
                profile =>
                    profile.ExpectedRuntimeHostId)
            .Any(
                group =>
                    group.Count() > 1))
        {
            throw new ArgumentException(
                "Enabled runtime-host profiles must not contain duplicate expected runtime-host identities.",
                nameof(profiles));
        }

        Profiles =
            Array.AsReadOnly(
                snapshot);
        profilesById =
            new Dictionary<
                RuntimeHostProfileId,
                RuntimeHostProfile>(
                snapshot.ToDictionary(
                    profile =>
                        profile.ProfileId));
    }

    public IReadOnlyList<RuntimeHostProfile> Profiles
    {
        get;
    }

    public bool TryGet(
        RuntimeHostProfileId profileId,
        out RuntimeHostProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(
            profileId);

        return profilesById.TryGetValue(
            profileId,
            out profile);
    }
}
