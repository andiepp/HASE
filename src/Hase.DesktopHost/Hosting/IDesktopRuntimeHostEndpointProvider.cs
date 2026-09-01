using Hase.CompactProtocol;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.DesktopHost.Hosting;

/// <summary>
/// Supplies an endpoint provider with the configured composition and the
/// host-owned inventory its attachments run against.
/// </summary>
public sealed class DesktopRuntimeHostEndpointProviderContext
{
    /// <summary>
    /// Initializes the resolution context for one runtime host start or
    /// endpoint refresh.
    /// </summary>
    public DesktopRuntimeHostEndpointProviderContext(
        DesktopRuntimeHostEndpointCompositionProfile endpointComposition,
        ICompactEndpointDefinitionRepository compactDefinitionRepository,
        IRuntimeEndpointAttachmentInventory attachmentInventory)
    {
        EndpointComposition =
            endpointComposition
            ?? throw new ArgumentNullException(nameof(endpointComposition));
        CompactDefinitionRepository =
            compactDefinitionRepository
            ?? throw new ArgumentNullException(
                nameof(compactDefinitionRepository));
        AttachmentInventory =
            attachmentInventory
            ?? throw new ArgumentNullException(nameof(attachmentInventory));
    }

    /// <summary>
    /// Gets the configured endpoint composition. A provider reads only the
    /// endpoints it supplies and ignores every other entry.
    /// </summary>
    public DesktopRuntimeHostEndpointCompositionProfile EndpointComposition
    {
        get;
    }

    /// <summary>
    /// Gets the compact endpoint definition repository owned by this host.
    /// </summary>
    /// <remarks>
    /// The repository is shared with the transport attachment host, so a
    /// compact-serial provider must resolve descriptors from it rather than
    /// from a repository of its own.
    /// </remarks>
    public ICompactEndpointDefinitionRepository CompactDefinitionRepository
    {
        get;
    }

    /// <summary>
    /// Gets the host-owned attachment inventory the contributed attachments
    /// run against.
    /// </summary>
    public IRuntimeEndpointAttachmentInventory AttachmentInventory
    {
        get;
    }
}

/// <summary>
/// Contributes one family of endpoints to a Desktop Runtime Host.
/// </summary>
/// <remarks>
/// A provider states which connection definitions it supports, creates the
/// attachment service for them when the host does not already route them, and
/// resolves the configured endpoints of its family into attachments. The host
/// library registers no provider of its own: an application composes the
/// providers it ships, exactly as the client library composes instrument
/// panels.
/// </remarks>
public interface IDesktopRuntimeHostEndpointProvider
{
    /// <summary>
    /// Gets the identifier this provider is registered under.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Indicates whether this provider supplies the endpoints reached through
    /// the supplied connection definition.
    /// </summary>
    bool Supports(IEndpointConnectionDefinition connectionDefinition);

    /// <summary>
    /// Creates the attachment service for this provider's connection
    /// definitions, or <see langword="null"/> when the runtime attachment
    /// host already routes them.
    /// </summary>
    IEndpointAttachmentService? CreateAttachmentService(
        RuntimeContext runtimeContext);

    /// <summary>
    /// Resolves the configured endpoints of this provider's family into
    /// attachments, performing any preflight the family requires.
    /// </summary>
    /// <remarks>
    /// Resolution attaches nothing. A provider with no configured endpoint
    /// resolves an empty list.
    /// </remarks>
    Task<IReadOnlyList<DesktopRuntimeHostEndpointAttachment>>
        ResolveAttachmentsAsync(
            DesktopRuntimeHostEndpointProviderContext context,
            CancellationToken cancellationToken = default);
}
