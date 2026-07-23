using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Provides immutable northbound snapshots of the runtime host's currently
/// published endpoint attachments.
/// </summary>
public interface IRuntimeHostInventorySnapshotProvider
{
    /// <summary>
    /// Returns a snapshot of the currently published endpoint attachments.
    /// </summary>
    IReadOnlyList<PublishedRuntimeEndpointSnapshot> List();

    /// <summary>
    /// Finds the currently published endpoint attachment with the supplied
    /// authoritative endpoint identity.
    /// </summary>
    PublishedRuntimeEndpointSnapshot? Find(
        EndpointId endpointId);
}