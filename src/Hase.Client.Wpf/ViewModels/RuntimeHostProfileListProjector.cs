using Hase.Client.Configuration;

namespace Hase.Client.Wpf.ViewModels;

public sealed class RuntimeHostProfileListProjector
{
    public IReadOnlyList<RuntimeHostProfileItemViewModel> Project(
        RuntimeHostProfileRegistry registry,
        MultiHostClientSessionSnapshot snapshot,
        RuntimeHostProfileId? selectedProfileId = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (selectedProfileId is not null && !registry.TryGet(selectedProfileId, out _))
            throw new ArgumentException("The selected runtime-host profile is not registered.", nameof(selectedProfileId));

        if (snapshot.Sessions.Count != registry.Profiles.Count)
            throw new ArgumentException("The multi-host snapshot must contain exactly one session for every registered profile.", nameof(snapshot));

        var sessions = snapshot.Sessions.ToDictionary(session => session.ProfileId);
        if (sessions.Keys.Any(id => !registry.TryGet(id, out _)))
            throw new ArgumentException("The multi-host snapshot contains an unregistered profile session.", nameof(snapshot));

        return Array.AsReadOnly(
            registry.Profiles.Select(profile =>
            {
                if (!sessions.TryGetValue(profile.ProfileId, out RuntimeHostProfileSessionSnapshot? session))
                    throw new ArgumentException("The multi-host snapshot is missing a registered profile session.", nameof(snapshot));
                return new RuntimeHostProfileItemViewModel(
                    profile.ProfileId,
                    profile.DisplayName,
                    profile.IsEnabled,
                    profile.ExpectedRuntimeHostId,
                    session.Status.State,
                    session.Status.RuntimeHostId,
                    session.Failure?.Category,
                    session.Failure?.Message,
                    session.ChangedAtUtc,
                    profile.ProfileId == selectedProfileId);
            }).ToArray());
    }
}
