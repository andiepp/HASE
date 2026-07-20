using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Owns the endpoint attachment inventory for one runtime context.
/// </summary>
/// <remarks>
/// The supplied attachment service must attach endpoints to the supplied
/// runtime context. Disposing the host disposes the complete attachment
/// inventory and therefore every attachment session owned by it.
/// </remarks>
public sealed class RuntimeEndpointAttachmentHost
    : IAsyncDisposable
{
    /// <summary>
    /// Initializes the endpoint attachment owner for one runtime context.
    /// </summary>
    public RuntimeEndpointAttachmentHost(
        RuntimeContext runtimeContext,
        IEndpointAttachmentService attachmentService)
    {
        RuntimeContext =
            runtimeContext
            ?? throw new ArgumentNullException(
                nameof(runtimeContext));

        ArgumentNullException.ThrowIfNull(
            attachmentService);

        AttachmentInventory =
            new RuntimeEndpointAttachmentInventory(
                attachmentService);
    }

    /// <summary>
    /// Gets the runtime context whose endpoint lifecycle is owned by this
    /// host.
    /// </summary>
    public RuntimeContext RuntimeContext
    {
        get;
    }

    /// <summary>
    /// Gets the host-owned endpoint attachment inventory.
    /// </summary>
    public IRuntimeEndpointAttachmentInventory AttachmentInventory
    {
        get;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return AttachmentInventory.DisposeAsync();
    }
}