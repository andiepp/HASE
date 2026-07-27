using Hase.Core.Domain.Identity;

namespace Hase.Client;

/// <summary>
/// Represents one immutable normalized snapshot of a remote HASE runtime host.
/// </summary>
public sealed record RemoteRuntimeHostSnapshot
{
    /// <summary>
    /// Initializes one normalized remote runtime-host snapshot.
    /// </summary>
    public RemoteRuntimeHostSnapshot(
        RemoteRuntimeHostId runtimeHostId,
        RuntimeHostClientApiVersion apiVersion,
        IEnumerable<RemoteEndpointAttachmentSnapshot> attachments)
    {
        RuntimeHostId =
            runtimeHostId
            ?? throw new ArgumentNullException(
                nameof(runtimeHostId));

        if (apiVersion.Major == 0)
        {
            throw new ArgumentException(
                "The remote API version must have a nonzero major version.",
                nameof(apiVersion));
        }

        ArgumentNullException.ThrowIfNull(
            attachments);

        RemoteEndpointAttachmentSnapshot[] attachmentSnapshot =
            attachments.ToArray();

        if (attachmentSnapshot.Any(
                attachment =>
                    attachment is null))
        {
            throw new ArgumentException(
                "The remote attachment collection must not contain null.",
                nameof(attachments));
        }

        EndpointId? duplicateEndpointId =
            attachmentSnapshot
                .GroupBy(
                    attachment =>
                        attachment.EndpointId)
                .FirstOrDefault(
                    group =>
                        group.Count() > 1)
                ?.Key;

        if (duplicateEndpointId is not null)
        {
            throw new ArgumentException(
                "The remote attachment collection must not contain more "
                + "than one current attachment for an endpoint identity.",
                nameof(attachments));
        }

        ApiVersion =
            apiVersion;

        Attachments =
            Array.AsReadOnly(
                attachmentSnapshot);
    }

    /// <summary>
    /// Gets the stable remote runtime-host identity.
    /// </summary>
    public RemoteRuntimeHostId RuntimeHostId
    {
        get;
    }

    /// <summary>
    /// Gets the remote API version represented by this snapshot.
    /// </summary>
    public RuntimeHostClientApiVersion ApiVersion
    {
        get;
    }

    /// <summary>
    /// Gets the immutable current published attachment collection.
    /// </summary>
    public IReadOnlyList<RemoteEndpointAttachmentSnapshot> Attachments
    {
        get;
    }
}
