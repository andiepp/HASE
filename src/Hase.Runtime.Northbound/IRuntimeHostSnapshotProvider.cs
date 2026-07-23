namespace Hase.Runtime.Northbound;

/// <summary>
/// Captures complete immutable northbound runtime-host snapshots.
/// </summary>
public interface IRuntimeHostSnapshotProvider
{
    /// <summary>
    /// Captures the current northbound runtime-host state.
    /// </summary>
    PublishedRuntimeHostSnapshot Capture();
}
