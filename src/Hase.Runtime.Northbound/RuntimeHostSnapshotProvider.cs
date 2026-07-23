namespace Hase.Runtime.Northbound;

/// <summary>
/// Captures complete northbound runtime-host snapshots from the authoritative
/// endpoint inventory projection.
/// </summary>
public sealed class RuntimeHostSnapshotProvider
    : IRuntimeHostSnapshotProvider
{
    private readonly IRuntimeHostInventorySnapshotProvider
        _inventorySnapshotProvider;

    /// <summary>
    /// Initializes a runtime-host snapshot provider.
    /// </summary>
    public RuntimeHostSnapshotProvider(
        IRuntimeHostInventorySnapshotProvider inventorySnapshotProvider)
    {
        _inventorySnapshotProvider =
            inventorySnapshotProvider
            ?? throw new ArgumentNullException(
                nameof(inventorySnapshotProvider));
    }

    /// <inheritdoc />
    public PublishedRuntimeHostSnapshot Capture()
    {
        return new PublishedRuntimeHostSnapshot(
            RuntimeHostApiVersion.Current,
            _inventorySnapshotProvider.List());
    }
}