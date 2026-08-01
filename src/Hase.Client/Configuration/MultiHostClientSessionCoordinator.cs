namespace Hase.Client.Configuration;

/// <summary>
/// Owns an ordered set of isolated runtime-host profile session controllers.
/// </summary>
public sealed class MultiHostClientSessionCoordinator
    : IMultiHostClientSessionCoordinator
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly RuntimeHostProfileRegistry registry;
    private readonly IReadOnlyDictionary<
        RuntimeHostProfileId,
        IRuntimeHostProfileSessionController> controllersByProfileId;
    private bool disposed;
    private MultiHostClientSessionSnapshot snapshot;

    public MultiHostClientSessionCoordinator(
        RuntimeHostProfileRegistry registry,
        IRuntimeHostProfileSessionControllerFactory controllerFactory)
    {
        this.registry = registry
            ?? throw new ArgumentNullException(nameof(registry));
        ArgumentNullException.ThrowIfNull(controllerFactory);

        var controllers =
            new Dictionary<RuntimeHostProfileId, IRuntimeHostProfileSessionController>();

        foreach (RuntimeHostProfile profile in registry.Profiles.Where(profile => profile.IsEnabled))
        {
            IRuntimeHostProfileSessionController controller =
                controllerFactory.Create(profile)
                ?? throw new InvalidOperationException(
                    "The profile session controller factory returned null.");

            if (controller.Snapshot.ProfileId != profile.ProfileId)
            {
                throw new InvalidOperationException(
                    "The profile session controller identity does not match the requested profile.");
            }

            controller.SnapshotChanged += ControllerSnapshotChanged;
            controllers.Add(profile.ProfileId, controller);
        }

        controllersByProfileId = controllers;
        snapshot = BuildSnapshot();
    }

    public event EventHandler? SnapshotChanged;

    public MultiHostClientSessionSnapshot Snapshot => Volatile.Read(ref snapshot);

    public async Task ConnectAsync(
        RuntimeHostProfileId profileId,
        CancellationToken cancellationToken = default)
    {
        IRuntimeHostProfileSessionController controller =
            await GetControllerAsync(profileId, cancellationToken).ConfigureAwait(false);
        await controller.ConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DisconnectAsync(RuntimeHostProfileId profileId)
    {
        IRuntimeHostProfileSessionController controller =
            await GetControllerAsync(profileId, CancellationToken.None).ConfigureAwait(false);
        await controller.DisconnectAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
        }
        finally
        {
            gate.Release();
        }

        var failures = new List<Exception>();

        foreach (IRuntimeHostProfileSessionController controller
            in controllersByProfileId.Values)
        {
            controller.SnapshotChanged -= ControllerSnapshotChanged;
            try
            {
                await controller.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "One or more runtime-host profile session controllers failed to dispose.",
                failures);
        }
    }

    private async Task<IRuntimeHostProfileSessionController> GetControllerAsync(
        RuntimeHostProfileId profileId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profileId);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (!registry.TryGet(profileId, out RuntimeHostProfile? profile))
            {
                throw new KeyNotFoundException(
                    $"Runtime-host profile '{profileId}' is not registered.");
            }

            if (!profile.IsEnabled)
            {
                throw new InvalidOperationException(
                    $"Runtime-host profile '{profileId}' is disabled.");
            }

            return controllersByProfileId[profileId];
        }
        finally
        {
            gate.Release();
        }
    }

    private void ControllerSnapshotChanged(object? sender, EventArgs args)
    {
        Volatile.Write(ref snapshot, BuildSnapshot());
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
    }

    private MultiHostClientSessionSnapshot BuildSnapshot() =>
        new(
            registry.Profiles.Select(
                profile =>
                    profile.IsEnabled
                        ? controllersByProfileId[profile.ProfileId].Snapshot
                        : new RuntimeHostProfileSessionSnapshot(
                            profile,
                            new RuntimeHostClientSessionStatus(
                                RuntimeHostClientSessionState.Disconnected),
                            DateTimeOffset.UtcNow)));
}
