using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Projects the authoritative runtime-host attachment inventory into
/// immutable northbound endpoint snapshots.
/// </summary>
/// <remarks>
/// One opaque generation is retained for the lifetime of each published
/// inventory-entry object. A later entry receives a new generation even when
/// it exposes the same authoritative endpoint identity.
/// </remarks>
public sealed class RuntimeHostInventorySnapshotProvider
    : IRuntimeHostInventorySnapshotProvider
{
    private readonly RuntimeHostAttachmentProjection
        _attachmentProjection;

    /// <summary>
    /// Initializes a northbound inventory snapshot provider.
    /// </summary>
    public RuntimeHostInventorySnapshotProvider(
        IRuntimeEndpointAttachmentInventory attachmentInventory)
        : this(
            new RuntimeHostAttachmentProjection(
                attachmentInventory))
    {
    }

    private RuntimeHostInventorySnapshotProvider(
        RuntimeHostAttachmentProjection attachmentProjection)
    {
        _attachmentProjection =
            attachmentProjection
            ?? throw new ArgumentNullException(
                nameof(attachmentProjection));
    }

    /// <summary>
    /// Creates a provider over a shared attachment projection.
    /// </summary>
    internal static RuntimeHostInventorySnapshotProvider CreateShared(
        RuntimeHostAttachmentProjection attachmentProjection)
    {
        return new RuntimeHostInventorySnapshotProvider(
            attachmentProjection);
    }

    /// <inheritdoc />
    public IReadOnlyList<PublishedRuntimeEndpointSnapshot> List()
    {
        IReadOnlyList<RuntimeHostPublishedAttachment> attachments =
            _attachmentProjection.List();

        var snapshots =
            new PublishedRuntimeEndpointSnapshot[attachments.Count];

        for (int index = 0; index < attachments.Count; index++)
        {
            RuntimeHostPublishedAttachment attachment =
                attachments[index];

            snapshots[index] =
                new PublishedRuntimeEndpointSnapshot(
                    attachment.Generation,
                    attachment.Entry.RuntimeEndpoint.Descriptor,
                    attachment.Entry.RuntimeEndpoint.ConnectionStatus);
        }

        return snapshots;
    }

    /// <inheritdoc />
    public PublishedRuntimeEndpointSnapshot? Find(
        EndpointId endpointId)
    {
        ArgumentNullException.ThrowIfNull(
            endpointId);

        return List().FirstOrDefault(
            snapshot =>
                snapshot.EndpointId == endpointId);
    }
}
