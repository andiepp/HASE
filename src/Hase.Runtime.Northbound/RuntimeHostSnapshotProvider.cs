namespace Hase.Runtime.Northbound;

/// <summary>
/// Captures complete northbound runtime-host snapshots from the authoritative
/// endpoint inventory projection.
/// </summary>
public sealed class RuntimeHostSnapshotProvider
    : IRuntimeHostSnapshotProvider
{
    private readonly RuntimeHostId
        _runtimeHostId;

    private readonly IRuntimeHostInventorySnapshotProvider
        _inventorySnapshotProvider;

    /// <summary>
    /// Initializes a runtime-host snapshot provider.
    /// </summary>
    public RuntimeHostSnapshotProvider(
        RuntimeHostId runtimeHostId,
        IRuntimeHostInventorySnapshotProvider inventorySnapshotProvider)
    {
        _runtimeHostId =
            runtimeHostId
            ?? throw new ArgumentNullException(
                nameof(runtimeHostId));

        _inventorySnapshotProvider =
            inventorySnapshotProvider
            ?? throw new ArgumentNullException(
                nameof(inventorySnapshotProvider));
    }

    /// <inheritdoc />
    public PublishedRuntimeHostSnapshot Capture()
    {
        return new PublishedRuntimeHostSnapshot(
            _runtimeHostId,
            RuntimeHostApiVersion.Current,
            _inventorySnapshotProvider.List());
    }
}