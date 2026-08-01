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
            controller.EventOccurred += ControllerEventOccurred;
            controllers.Add(profile.ProfileId, controller);
        }

        controllersByProfileId = controllers;
        snapshot = BuildSnapshot();
    }

    public event EventHandler? SnapshotChanged;
    public event EventHandler<RuntimeHostProfileEventOccurredEventArgs>? EventOccurred;

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

    public async Task<RemotePropertyOperationResult> ReadPropertyAsync(RemoteRuntimeHostPropertyTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        IRuntimeHostProfileSessionController controller = await GetConnectedControllerAsync(target.RuntimeHostId, cancellationToken).ConfigureAwait(false);
        return await controller.ReadPropertyAsync(target.Target, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RemotePropertyOperationResult> WritePropertyAsync(RemoteRuntimeHostPropertyTarget target, RemoteValue requestedValue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(requestedValue);
        IRuntimeHostProfileSessionController controller = await GetConnectedControllerAsync(target.RuntimeHostId, cancellationToken).ConfigureAwait(false);
        return await controller.WritePropertyAsync(target.Target, requestedValue, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RemoteCommandOperationResult> ExecuteCommandAsync(RemoteRuntimeHostCommandExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IRuntimeHostProfileSessionController controller = await GetConnectedControllerAsync(request.RuntimeHostId, cancellationToken).ConfigureAwait(false);
        return await controller.ExecuteCommandAsync(request.Request, cancellationToken).ConfigureAwait(false);
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
            controller.EventOccurred -= ControllerEventOccurred;
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

    private async Task<IRuntimeHostProfileSessionController> GetConnectedControllerAsync(
        RemoteRuntimeHostId runtimeHostId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtimeHostId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            IRuntimeHostProfileSessionController[] matches = controllersByProfileId.Values
                .Where(controller =>
                    controller.Snapshot.Status.State == RuntimeHostClientSessionState.Connected
                    && controller.Snapshot.Status.RuntimeHostId == runtimeHostId)
                .ToArray();
            return matches.Length switch
            {
                1 => matches[0],
                0 => throw new KeyNotFoundException($"No connected runtime-host session matches '{runtimeHostId}'."),
                _ => throw new InvalidOperationException($"More than one connected session matches runtime host '{runtimeHostId}'.")
            };
        }
        finally { gate.Release(); }
    }

    private void ControllerSnapshotChanged(object? sender, EventArgs args)
    {
        Volatile.Write(ref snapshot, BuildSnapshot());
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ControllerEventOccurred(object? sender, RuntimeHostProfileEventOccurredEventArgs args)
    {
        if (sender is not IRuntimeHostProfileSessionController controller
            || controller.Snapshot.ProfileId != args.ProfileId)
            throw new InvalidOperationException("The Event profile does not match its session controller.");
        EventOccurred?.Invoke(this, args);
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
