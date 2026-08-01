namespace Hase.Client.Configuration;

/// <summary>Creates one unconnected client session for a resolved profile.</summary>
public interface IRuntimeHostProfileClientSessionFactory
{
    Task<IRuntimeHostClientSession> CreateAsync(
        RuntimeHostProfileId profileId,
        CancellationToken cancellationToken = default);
}
