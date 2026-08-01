namespace Hase.Client.Configuration;

/// <summary>
/// Holds one immutable ordered snapshot of independently managed runtime-host
/// profile sessions.
/// </summary>
public sealed class MultiHostClientSessionSnapshot
{
    private readonly IReadOnlyDictionary<
        RuntimeHostProfileId,
        RuntimeHostProfileSessionSnapshot> sessionsByProfileId;

    public MultiHostClientSessionSnapshot(
        IEnumerable<RuntimeHostProfileSessionSnapshot> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        RuntimeHostProfileSessionSnapshot[] snapshot = sessions.ToArray();

        if (snapshot.Any(session => session is null))
        {
            throw new ArgumentException(
                "A multi-host session snapshot must not contain null.",
                nameof(sessions));
        }

        if (snapshot
            .GroupBy(session => session.ProfileId)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "A multi-host session snapshot must not contain duplicate profile identities.",
                nameof(sessions));
        }

        Sessions = Array.AsReadOnly(snapshot);
        sessionsByProfileId =
            new Dictionary<RuntimeHostProfileId, RuntimeHostProfileSessionSnapshot>(
                snapshot.ToDictionary(session => session.ProfileId));
    }

    public IReadOnlyList<RuntimeHostProfileSessionSnapshot> Sessions { get; }

    public bool TryGet(
        RuntimeHostProfileId profileId,
        out RuntimeHostProfileSessionSnapshot? session)
    {
        ArgumentNullException.ThrowIfNull(profileId);
        return sessionsByProfileId.TryGetValue(profileId, out session);
    }
}
