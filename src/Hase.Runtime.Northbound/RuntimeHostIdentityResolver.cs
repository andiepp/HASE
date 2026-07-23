namespace Hase.Runtime.Northbound;

/// <summary>
/// Resolves authoritative runtime-host identity according to configured,
/// persisted, and generated-and-persisted precedence.
/// </summary>
public sealed class RuntimeHostIdentityResolver
    : IRuntimeHostIdentityResolver
{
    private readonly IRuntimeHostIdentityStore
        _identityStore;

    private readonly IRuntimeHostIdGenerator
        _identityGenerator;

    /// <summary>
    /// Initializes a runtime-host identity resolver.
    /// </summary>
    public RuntimeHostIdentityResolver(
        IRuntimeHostIdentityStore identityStore,
        IRuntimeHostIdGenerator identityGenerator)
    {
        _identityStore =
            identityStore
            ?? throw new ArgumentNullException(
                nameof(identityStore));

        _identityGenerator =
            identityGenerator
            ?? throw new ArgumentNullException(
                nameof(identityGenerator));
    }

    /// <inheritdoc />
    public async Task<RuntimeHostIdentityResolution> ResolveAsync(
        RuntimeHostId? configuredRuntimeHostId = null,
        CancellationToken cancellationToken = default)
    {
        if (configuredRuntimeHostId is not null)
        {
            return new RuntimeHostIdentityResolution(
                configuredRuntimeHostId,
                RuntimeHostIdentityOrigin.ExplicitConfiguration);
        }

        RuntimeHostId? persistedRuntimeHostId =
            await _identityStore
                .ReadAsync(
                    cancellationToken)
                .ConfigureAwait(
                    false);

        if (persistedRuntimeHostId is not null)
        {
            return new RuntimeHostIdentityResolution(
                persistedRuntimeHostId,
                RuntimeHostIdentityOrigin.Persisted);
        }

        RuntimeHostId candidate =
            _identityGenerator.Generate();

        RuntimeHostIdentityStoreCreateResult createResult =
            await _identityStore
                .CreateIfMissingAsync(
                    candidate,
                    cancellationToken)
                .ConfigureAwait(
                    false);

        RuntimeHostIdentityOrigin origin =
            createResult.Outcome
                == RuntimeHostIdentityStoreCreateOutcome.Created
                    ? RuntimeHostIdentityOrigin.GeneratedAndPersisted
                    : RuntimeHostIdentityOrigin.Persisted;

        return new RuntimeHostIdentityResolution(
            createResult.RuntimeHostId,
            origin);
    }
}