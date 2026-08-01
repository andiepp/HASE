using Hase.Client.Configuration;

namespace Hase.Client.Grpc.Configuration;

/// <summary>Resolves one strict registry profile into the existing single-host session factory.</summary>
public sealed class PrivateNetworkRuntimeHostProfileClientSessionFactory
    : IRuntimeHostProfileClientSessionFactory
{
    private readonly PrivateNetworkRuntimeHostProfileRegistry registry;
    private readonly IRuntimeHostClientSessionFactory sessionFactory;

    public PrivateNetworkRuntimeHostProfileClientSessionFactory(
        PrivateNetworkRuntimeHostProfileRegistry registry,
        IRuntimeHostClientSessionFactory sessionFactory)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    }

    public Task<IRuntimeHostClientSession> CreateAsync(
        RuntimeHostProfileId profileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profileId);
        if (!registry.TryGet(profileId, out PrivateNetworkRuntimeHostProfile? profile))
            throw new KeyNotFoundException($"Runtime-host profile '{profileId}' is not registered.");
        if (!profile.Profile.IsEnabled)
            throw new InvalidOperationException($"Runtime-host profile '{profileId}' is disabled.");
        return sessionFactory.CreateAsync(profile.PrivateNetworkConfigurationFilePath, cancellationToken);
    }
}
